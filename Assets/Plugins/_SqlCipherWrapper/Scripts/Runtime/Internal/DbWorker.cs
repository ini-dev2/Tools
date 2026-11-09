#region USINGS

using Cysharp.Threading.Tasks;
using SqlCipher4Unity3D;
using System;
using System.Collections.Concurrent;
using System.Threading;

#endregion USINGS


namespace SqlCipherWrapper.Runtime
{
    /// <summary>
    /// Выделенный фоновый поток, единолично владеющий SQLiteConnection.
    ///
    /// Все операции сериализуются через BlockingCollection.
    /// Потокобезопасность гарантирована тем, что соединение
    /// НИКОГДА не покидает этот поток.
    ///
    /// Жизненный цикл:
    ///   Created > Starting > Running > Stopping > Stopped
    ///                    ? FatalError ?
    /// </summary>
    internal sealed class DbWorker : IDisposable
    {
        private readonly DbWorkerOptions _options;
        private readonly IDbLogger _logger;
        private readonly BlockingCollection<WorkItemBase> _queue;
        private readonly CancellationTokenSource _shutdownCts;

        private Thread _thread;
        private SQLiteConnection _connection;
        private Exception _fatalError;
        private volatile WorkerState _state;
        private volatile bool _disposed;

        public WorkerState State => _state;
        public bool IsRunning => _state == WorkerState.Running;
        public int QueueCount => _queue.Count;

        public DbWorker(DbWorkerOptions options = null, IDbLogger logger = null)
        {
            _options = options ?? DbWorkerOptions.Default;
            _logger = logger ?? NullDbLogger.Instance;

            _queue = _options.QueueCapacity > 0
                ? new BlockingCollection<WorkItemBase>(
                    new ConcurrentQueue<WorkItemBase>(),
                    _options.QueueCapacity)
                : new BlockingCollection<WorkItemBase>(
                    new ConcurrentQueue<WorkItemBase>());

            _shutdownCts = new CancellationTokenSource();
            _state = WorkerState.Created;
        }

        /// <summary>
        /// Запускает поток. Соединение создаётся ВНУТРИ потока,
        /// чтобы гарантировать, что оно никогда не покинет его.
        /// </summary>
        public async UniTask StartAsync(
            Func<SQLiteConnection> connectionFactory,
            CancellationToken ct = default)
        {
            if (connectionFactory == null)
                throw new ArgumentNullException(nameof(connectionFactory));

            ThrowIfDisposed();

            if (_state != WorkerState.Created)
                throw new InvalidOperationException(
                    $"Cannot start worker in state {_state}");

            _state = WorkerState.Starting;

            _thread = new Thread(() => WorkerLoop(connectionFactory))
            {
                Name = _options.ThreadName,
                IsBackground = true,
                Priority = _options.ThreadPriority
            };

            _thread.Start();

            await WaitUntilRunningAsync(ct);

            if (_state == WorkerState.FatalError)
                throw new InvalidOperationException(
                    "DbWorker failed to start", _fatalError);
        }

        private async UniTask WaitUntilRunningAsync(CancellationToken ct)
        {
            var timeout = TimeSpan.FromSeconds(30);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            while (_state == WorkerState.Starting)
            {
                if (ct.IsCancellationRequested)
                    throw new OperationCanceledException(ct);

                if (sw.Elapsed > timeout)
                    throw new TimeoutException(
                        "DbWorker did not start within 30 seconds");

                await UniTask.Delay(10, cancellationToken: ct);
            }
        }

        /// <summary>
        /// Ставит операцию в очередь. Безопасно из любого потока.
        /// </summary>
        public async UniTask<T> EnqueueAsync<T>(
            Func<SQLiteConnection, T> operation,
            CancellationToken ct = default,
            string description = null)
        {
            ThrowIfDisposed();
            ThrowIfFatal();

            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var item = new WorkItem<T>(operation, ct, description);

            try
            {
                _queue.Add(item, _shutdownCts.Token);
            }
            catch (InvalidOperationException)
            {
                throw new ObjectDisposedException(nameof(DbWorker));
            }
            catch (OperationCanceledException)
            {
                throw new ObjectDisposedException(nameof(DbWorker));
            }

            if (_options.VerboseLogging)
                _logger.Info(
                    $"Enqueued: {item.Description} (queue={_queue.Count})");

            return await item.Task.AsUniTask();
        }

        /// <summary>
        /// Версия без результата. Использует Unit для единообразия.
        /// </summary>
        public async UniTask EnqueueVoidAsync(
                                Action<SQLiteConnection> operation,
                                CancellationToken ct = default,
                                string description = null)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            ThrowIfDisposed();
            ThrowIfFatal();

            var item = new WorkItem<object>(
                conn => { operation(conn); return null; },
                ct,
                description);

            try
            {
                _queue.Add(item, _shutdownCts.Token);
            }
            catch (InvalidOperationException)
            {
                throw new ObjectDisposedException(nameof(DbWorker));
            }
            catch (OperationCanceledException)
            {
                throw new ObjectDisposedException(nameof(DbWorker));
            }

            if (_options.VerboseLogging)
                _logger.Info($"Enqueued: {item.Description} (queue={_queue.Count})");

            await item.Task.AsUniTask();
        }

        private void WorkerLoop(Func<SQLiteConnection> connectionFactory)
        {
            try
            {
                // Открываем соединение ВНУТРИ потока.
                _connection = connectionFactory();

                // Базовая настройка SQLite.
                ConfigureConnection(_connection);

                _state = WorkerState.Running;
                _logger.Info("DbWorker started");

                // Основной цикл: блокируемся на Take, пока не придут элементы
                // или не вызовут CompleteAdding.
                foreach (var item in _queue.GetConsumingEnumerable(
                    _shutdownCts.Token))
                {
                    ExecuteItem(item);
                }
            }
            catch (OperationCanceledException)
            {
                // Нормальное завершение через _shutdownCts.Cancel()
                _logger.Info("DbWorker shutdown requested");
            }
            catch (Exception ex)
            {
                _logger.Error("DbWorker fatal error in loop", ex);
                _fatalError = ex;
                _state = WorkerState.FatalError;

                // Завершаем все оставшиеся элементы, чтобы клиенты не висели.
                while (_queue.TryTake(out var pending))
                    pending.Fail(ex);
            }
            finally
            {
                SafeDisposeConnection();
                _state = WorkerState.Stopped;
                _logger.Info("DbWorker stopped");
            }
        }

        private void ExecuteItem(WorkItemBase item)
        {
            // Проверяем фатальную ошибку перед каждой операцией.
            if (_fatalError != null)
            {
                item.Fail(new InvalidOperationException(
                    "DbWorker is in a fatal error state", _fatalError));
                return;
            }

            var sw = _options.VerboseLogging
                ? System.Diagnostics.Stopwatch.StartNew()
                : null;

            try
            {
                item.Execute(_connection);

                if (sw != null)
                {
                    sw.Stop();
                    _logger.Info(
                        $"Completed: {item.Description} " +
                        $"in {sw.ElapsedMilliseconds}ms");
                }
            }
            catch (Exception ex)
            {
                // Ошибка уровня ОПЕРАЦИИ, не воркера.
                // Воркер продолжает работать — это важно:
                // одна кривая вставка не должна убивать всю БД.
                _logger.Error($"Operation failed: {item.Description}", ex);
                item.Fail(ex);
            }
        }

        private void ConfigureConnection(SQLiteConnection connection)
        {
            // Все PRAGMA читаем через Query — безопасно для любых версий,
            // потому что Query корректно обрабатывает и SQLITE_ROW, и SQLITE_DONE.
            ApplyPragma(connection, "journal_mode", "WAL");
            ApplyPragma(connection, "synchronous", "NORMAL");
            ApplyPragma(connection, "foreign_keys", "ON");
            ApplyPragma(connection, "cache_size", "-8000");
            ApplyPragma(connection, "busy_timeout", "5000");
            ApplyPragma(connection, "temp_store", "MEMORY");
        }

        private void ApplyPragma(SQLiteConnection conn, string name, string value)
        {
            try
            {
                var result = conn.Query<object>($"PRAGMA {name} = {value};");
                _logger.Info($"PRAGMA {name} = {value} → {result.Count} rows");
            }
            catch (Exception ex)
            {
                _logger.Warning($"PRAGMA {name} = {value} failed: {ex.Message}");
            }
        }

        private void SafeDisposeConnection()
        {
            try
            {
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to dispose connection", ex);
            }
            finally
            {
                _connection = null;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DbWorker));
        }

        private void ThrowIfFatal()
        {
            if (_fatalError != null)
                throw new InvalidOperationException(
                    "DbWorker is in a fatal error state", _fatalError);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _state = WorkerState.Stopping;

            try
            {
                // 1. Запрещаем новые элементы.
                _queue.CompleteAdding();

                // 2. Просим поток завершиться.
                _shutdownCts.Cancel();

                // 3. Ждём завершения с таймаутом.
                if (_thread != null &&
                    !_thread.Join(_options.ShutdownTimeout))
                {
                    _logger.Warning(
                        $"DbWorker thread did not finish within " +
                        $"{_options.ShutdownTimeout.TotalSeconds}s. " +
                        "It may be stuck in a long-running SQLite operation.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("DbWorker dispose failed", ex);
            }
            finally
            {
                try { _queue.Dispose(); } catch { }
                try { _shutdownCts.Dispose(); } catch { }
                _state = WorkerState.Stopped;
            }
        }
    }
}
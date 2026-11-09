#region USINGS

#endregion USINGS

using SqlCipher4Unity3D;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlCipherWrapper.Runtime
{
    /// <summary>
    /// Одна операция над SQLiteConnection.
    /// Выполняется в потоке-воркере, результат возвращается через TaskCompletionSource.
    /// </summary>
    internal sealed class WorkItem<T> : WorkItemBase
    {
        private readonly Func<SQLiteConnection, T> _operation;
        private readonly CancellationToken _ct;
        private readonly TaskCompletionSource<T> _tcs;
        private readonly CancellationTokenRegistration _ctRegistration;

        public Task<T> Task => _tcs.Task;

        public WorkItem(
            Func<SQLiteConnection, T> operation,
            CancellationToken ct,
            string description)
        {
            _operation = operation
                ?? throw new ArgumentNullException(nameof(operation));
            _ct = ct;
            Description = description ?? "DB operation";

            // КРИТИЧНО: RunContinuationsAsynchronously.
            // Без этого await-продолжения клиента выполнятся в потоке воркера,
            // что уронит Unity API (не главный поток).
            _tcs = new TaskCompletionSource<T>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            // Отмена: если клиент отменил — сразу завершаем Task.
            // Сама операция не прервётся (SQLite не поддерживает),
            // но клиент не будет ждать результата.
            if (ct.CanBeCanceled)
            {
                _ctRegistration = ct.Register(
                    () => _tcs.TrySetCanceled(ct));
            }
        }

        public override void Execute(SQLiteConnection connection)
        {
            if (_ct.IsCancellationRequested)
            {
                _tcs.TrySetCanceled(_ct);
                return;
            }

            try
            {
                var result = _operation(connection);
                _tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
            }
            finally
            {
                // Освобождаем регистрацию в CancellationToken.
                _ctRegistration.Dispose();
            }
        }

        public override void Fail(Exception ex)
        {
            _tcs.TrySetException(ex);
            _ctRegistration.Dispose();
        }
    }
}
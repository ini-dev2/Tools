#region USINGS

using Cysharp.Threading.Tasks;
using SqlCipher4Unity3D;
using System;
using System.Collections.Generic;

#endregion USINGS

namespace SqlCipherWrapper.Runtime
{
    public sealed class DbService : IDbService
    {
        private readonly DbWorker _worker;
        private readonly IDbLogger _logger;
        private bool _disposed;

        internal DbService(DbWorker worker, IDbLogger logger)
        {
            _worker = worker ?? throw new ArgumentNullException(nameof(worker));
            _logger = logger ?? NullDbLogger.Instance;
        }

        // === Schema ===

        public UniTask CreateTableAsync<TEntity>() where TEntity : DbEntity
        {
            return _worker.EnqueueVoidAsync(
                conn => conn.CreateTable<TEntity>(),
                description: $"CreateTable<{typeof(TEntity).Name}>");
        }

        public UniTask DropTableAsync<TEntity>() where TEntity : DbEntity
        {
            return _worker.EnqueueVoidAsync(
                conn => conn.DropTable<TEntity>(),
                description: $"DropTable<{typeof(TEntity).Name}>");
        }

        // === CRUD ===

        public UniTask CreateAsync<TEntity>(TEntity data) where TEntity : DbEntity
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return _worker.EnqueueVoidAsync(
                conn => conn.Insert(data),
                description: $"Insert<{typeof(TEntity).Name}>");
        }

        public UniTask<TEntity> GetByIdAsync<TEntity>(int id)
            where TEntity : DbEntity, new()
        {
            return _worker.EnqueueAsync(
                conn => conn.Table<TEntity>().FirstOrDefault(x => x.Id == id),
                description: $"GetById<{typeof(TEntity).Name}>({id})");
        }

        public UniTask<IReadOnlyList<TEntity>> GetAllAsync<TEntity>()
            where TEntity : DbEntity, new()
        {
            return _worker.EnqueueAsync<IReadOnlyList<TEntity>>(
                conn => conn.Table<TEntity>().ToList(),
                description: $"GetAll<{typeof(TEntity).Name}>");
        }

        public UniTask<bool> UpdateAsync<TEntity>(TEntity data)
            where TEntity : DbEntity
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return _worker.EnqueueAsync(
                conn => conn.Update(data) > 0,
                description: $"Update<{typeof(TEntity).Name}>({data.Id})");
        }

        public UniTask<bool> DeleteByIdAsync<TEntity>(int id)
            where TEntity : DbEntity
        {
            return _worker.EnqueueAsync(
                conn => conn.Delete<TEntity>(id) > 0,
                description: $"Delete<{typeof(TEntity).Name}>({id})");
        }

        public UniTask<int> CountAsync<TEntity>()
            where TEntity : DbEntity, new()
        {
            return _worker.EnqueueAsync(
                conn => conn.Table<TEntity>().Count(),
                description: $"Count<{typeof(TEntity).Name}>");
        }

        // === Batch ===
        public UniTask RunInTransactionAsync(Action<SQLiteConnection> operations)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));

            return _worker.EnqueueVoidAsync(
                conn =>
                {
                    conn.BeginTransaction();
                    try
                    {
                        operations(conn);
                        conn.Commit();
                    }
                    catch
                    {
                        conn.Rollback();
                        throw;
                    }
                },
                description: "Transaction");
        }

        // === Raw SQL ===
        public UniTask<int> ExecuteAsync(string sql, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL is required", nameof(sql));

            return _worker.EnqueueAsync(
                conn => conn.Execute(sql, args),
                description: "Execute raw SQL");
        }

        public UniTask<List<T>> QueryAsync<T>(string sql, params object[] args)
            where T : new()
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL is required", nameof(sql));

            return _worker.EnqueueAsync(
                conn => conn.Query<T>(sql, args),
                description: $"Query<{typeof(T).Name}>");
        }

        // === Diagnostics ===

        public WorkerState State => _worker.State;
        public int PendingOperations => _worker.QueueCount;

        // === Lifecycle ===

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _worker.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
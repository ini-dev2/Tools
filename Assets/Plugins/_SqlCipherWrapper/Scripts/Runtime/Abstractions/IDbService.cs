#region USINGS

using Cysharp.Threading.Tasks;
using SqlCipher4Unity3D;
using System;
using System.Collections.Generic;

#endregion USINGS

namespace SqlCipherWrapper.Runtime
{
    /// <summary>
    /// Публичный API для работы с БД.
    /// Все методы асинхронны и потокобезопасны.
    /// </summary>
    public interface IDbService : IDisposable
    {
        // === Schema ===

        UniTask CreateTableAsync<TEntity>() where TEntity : DbEntity;
        UniTask DropTableAsync<TEntity>() where TEntity : DbEntity;

        // === CRUD ===

        UniTask CreateAsync<TEntity>(TEntity data) where TEntity : DbEntity;
        UniTask<TEntity> GetByIdAsync<TEntity>(int id)
            where TEntity : DbEntity, new();
        UniTask<IReadOnlyList<TEntity>> GetAllAsync<TEntity>()
            where TEntity : DbEntity, new();
        UniTask<bool> UpdateAsync<TEntity>(TEntity data) where TEntity : DbEntity;
        UniTask<bool> DeleteByIdAsync<TEntity>(int id) where TEntity : DbEntity;
        UniTask<int> CountAsync<TEntity>() where TEntity : DbEntity, new();

        // === Batch ===

        /// <summary>
        /// Выполняет несколько операций в одной транзакции.
        /// Атомарно: либо всё, либо ничего. Быстрее в 10-100 раз для массовых вставок.
        /// </summary>
        UniTask RunInTransactionAsync(Action<SQLiteConnection> operations);

        // === Raw SQL ===

        UniTask<int> ExecuteAsync(string sql, params object[] args);
        UniTask<List<T>> QueryAsync<T>(string sql, params object[] args)
            where T : new();

        // === Diagnostics ===

        WorkerState State { get; }
        int PendingOperations { get; }
    }
}
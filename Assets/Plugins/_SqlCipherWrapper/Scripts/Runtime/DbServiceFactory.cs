#region USINGS

using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

#endregion USINGS

namespace SqlCipherWrapper.Runtime
{
    /// <summary>
    /// Фабрика DbService. Инкапсулирует асинхронную инициализацию,
    /// чтобы клиент не мог забыть вызвать InitializeAsync.
    ///
    /// Использование:
    ///   var db = await DbServiceFactory.CreateAsync("game.db", "password");
    /// </summary>
    public static class DbServiceFactory
    {
        public static async UniTask<DbService> CreateAsync(
            string dbName,
            string dbPassword,
            IDbLogger logger = null,
            DbWorkerOptions options = null,
            IDbConnectionFactory connectionFactory = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dbName))
                throw new ArgumentException(
                    "Database name is required", nameof(dbName));
            if (string.IsNullOrWhiteSpace(dbPassword))
                throw new ArgumentException(
                    "Password is required", nameof(dbPassword));

            logger ??= NullDbLogger.Instance;
            options ??= DbWorkerOptions.Default;
            connectionFactory ??= new DefaultConnectionFactory();

            var path = await GetOrCreateDatabasePathAsync(
                dbName, logger, ct);

            var worker = new DbWorker(options, logger);

            try
            {
                await worker.StartAsync(
                    () => connectionFactory.Create(path, dbPassword),
                    ct);
            }
            catch
            {
                worker.Dispose();
                throw;
            }

            return new DbService(worker, logger);
        }

        private static UniTask<string> GetOrCreateDatabasePathAsync(
                                                    string databaseName,
                                                    IDbLogger logger,
                                                    CancellationToken ct)
        {
            var persistentPath = Path.Combine(
                Application.persistentDataPath, databaseName);

            // Создаём папку, если её нет.
            var dir = Path.GetDirectoryName(persistentPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(persistentPath))
            {
                logger.Info($"Using existing DB at {persistentPath}");
            }
            else
            {
                logger.Info($"Will create new DB at {persistentPath}");
            }

            return UniTask.FromResult(persistentPath);
        }
    }
}
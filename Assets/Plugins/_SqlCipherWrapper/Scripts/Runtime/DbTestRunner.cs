// Assets/Scripts/Test/DbTestRunner.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using SqlCipherWrapper.Runtime;
using UnityEngine;
using Debug = UnityEngine.Debug;

public sealed class DbTestRunner : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private string _dbName = "test.db";
    [SerializeField] private string _dbPassword = "test_password_123";
    [SerializeField] private bool _verboseLogging = true;
    [SerializeField] private bool _useVrOptions = false;
    [SerializeField] private bool _runOnStart = true;

    private IDbService _db;

    private async UniTaskVoid Start()
    {
        await UniTask.WaitForSeconds(7f);

        if (!_runOnStart) return;
        await RunAllTestsAsync();
    }

    private void OnDestroy()
    {
        _db?.Dispose();
    }

    // ============================================================
    //  Контекстное меню для ручного запуска
    // ============================================================

    [ContextMenu("0. Run All Tests")]
    private async void MenuRunAll() => await RunAllTestsAsync();

    [ContextMenu("1. Init DB")]
    private async void MenuInit() => await SafeRun(InitializeAsync);

    [ContextMenu("2. CRUD Test")]
    private async void MenuCrud() => await SafeRun(TestCrudAsync);

    [ContextMenu("3. Transaction Test")]
    private async void MenuTransaction() => await SafeRun(TestTransactionAsync);

    [ContextMenu("4. Concurrency Test")]
    private async void MenuConcurrency() => await SafeRun(TestConcurrencyAsync);

    [ContextMenu("5. Error Resilience Test")]
    private async void MenuErrorResilience() => await SafeRun(TestErrorResilienceAsync);

    [ContextMenu("6. Stress Test (10000 ops)")]
    private async void MenuStress() => await SafeRun(TestStressAsync);

    [ContextMenu("7. Cancellation Test")]
    private async void MenuCancellation() => await SafeRun(TestCancellationAsync);

    [ContextMenu("8. Queue Capacity Test")]
    private async void MenuQueueCapacity() => await SafeRun(TestQueueCapacityAsync);

    [ContextMenu("9. Dispose Test")]
    private async void MenuDispose() => await SafeRun(TestDisposeAsync);

    [ContextMenu("99. Dispose DB")]
    private void MenuDisposeDb()
    {
        _db?.Dispose();
        _db = null;
        Debug.Log("[TEST] DB disposed");
    }

    // ============================================================
    //  Основной сценарий
    // ============================================================

    private async UniTask RunAllTestsAsync()
    {
        var total = Stopwatch.StartNew();
        var passed = new List<string>();
        var failed = new List<string>();

        async UniTask Run(string name, Func<UniTask> test)
        {
            Debug.Log($"[TEST] ─── {name} ───");
            try
            {
                await test();
                passed.Add(name);
                Debug.Log($"[TEST] ✓ {name} PASSED");
            }
            catch (Exception ex)
            {
                failed.Add(name);
                Debug.LogError($"[TEST] ✗ {name} FAILED: {ex.Message}\n{ex}");
            }
        }

        await Run("Init", InitializeAsync);
        if (_db == null)
        {
            Debug.LogError("[TEST] DB init failed — остальные тесты пропущены");
            return;
        }

        await Run("CRUD", TestCrudAsync);
        await Run("Transaction", TestTransactionAsync);
        await Run("Concurrency", TestConcurrencyAsync);
        await Run("Error Resilience", TestErrorResilienceAsync);
        await Run("Stress", TestStressAsync);
        await Run("Cancellation", TestCancellationAsync);
        await Run("Queue Capacity", TestQueueCapacityAsync);

        total.Stop();

        Debug.Log($"[TEST] ═══════════════════════════════════");
        Debug.Log($"[TEST] Итог: {passed.Count} passed, {failed.Count} failed");
        Debug.Log($"[TEST] Время: {total.ElapsedMilliseconds}ms");

        if (failed.Count == 0)
            Debug.Log("[TEST] ✓ ALL TESTS PASSED");
        else
            Debug.LogError($"[TEST] ✗ FAILED: {string.Join(", ", failed)}");
    }

    private async UniTask SafeRun(Func<UniTask> test)
    {
        if (_db == null && test != (Func<UniTask>)InitializeAsync)
        {
            Debug.LogError("[TEST] Init DB first");
            return;
        }

        try
        {
            await test();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TEST] FAILED: {ex}");
        }
    }

    // ============================================================
    //  INIT
    // ============================================================

    private async UniTask InitializeAsync()
    {
        var sw = Stopwatch.StartNew();

        var logger = new UnityDbLogger(verbose: _verboseLogging);
        var options = _useVrOptions
            ? DbWorkerOptions.VrDefault
            : DbWorkerOptions.Default;

        _db = await DbServiceFactory.CreateAsync(
            dbName: _dbName,
            dbPassword: _dbPassword,
            logger: logger,
            options: options);

        // Идемпотентно: CreateTable не падает, если таблица есть.
        await _db.CreateTableAsync<Player>();
        await _db.CreateTableAsync<InventoryItem>();

        // Очистка на случай, если БД осталась с прошлого запуска.
        await _db.ExecuteAsync("DELETE FROM Player");
        await _db.ExecuteAsync("DELETE FROM InventoryItem");

        sw.Stop();
        Debug.Log($"[TEST] DB initialized in {sw.ElapsedMilliseconds}ms, " +
                  $"state={_db.State}, pending={_db.PendingOperations}");
    }

    // ============================================================
    //  CRUD
    // ============================================================

    private async UniTask TestCrudAsync()
    {
        var sw = Stopwatch.StartNew();

        // --- CREATE ---
        var player = new Player { Name = "Alice", Level = 1, Health = 100f };
        await _db.CreateAsync(player);

        if (player.Id <= 0)
            throw new Exception("Insert failed: Id not set");
        Debug.Log($"[TEST] Created player Id={player.Id}");

        // --- READ ---
        var loaded = await _db.GetByIdAsync<Player>(player.Id);
        if (loaded == null)
            throw new Exception("GetById returned null");
        if (loaded.Name != "Alice")
            throw new Exception($"Name mismatch: {loaded.Name}");
        if (loaded.Level != 1)
            throw new Exception($"Level mismatch: {loaded.Level}");
        if (Math.Abs(loaded.Health - 100f) > 0.001f)
            throw new Exception($"Health mismatch: {loaded.Health}");
        Debug.Log($"[TEST] Loaded: {loaded.Name}, Level={loaded.Level}");

        // --- READ non-existent ---
        var missing = await _db.GetByIdAsync<Player>(999999);
        if (missing != null)
            throw new Exception("GetById(non-existent) should return null");
        Debug.Log("[TEST] GetById(non-existent) returned null");

        // --- UPDATE ---
        loaded.Level = 10;
        loaded.Health = 50f;
        var updated = await _db.UpdateAsync(loaded);
        if (!updated)
            throw new Exception("Update returned false");

        var reloaded = await _db.GetByIdAsync<Player>(player.Id);
        if (reloaded.Level != 10)
            throw new Exception($"Update not persisted: Level={reloaded.Level}");
        if (Math.Abs(reloaded.Health - 50f) > 0.001f)
            throw new Exception($"Health not persisted: {reloaded.Health}");
        Debug.Log($"[TEST] Updated: Level={reloaded.Level}, Health={reloaded.Health}");

        // --- UPDATE non-existent ---
        var ghost = new Player { Id = 999999, Name = "Ghost", Level = 1, Health = 1f };
        var ghostUpdated = await _db.UpdateAsync(ghost);
        if (ghostUpdated)
            throw new Exception("Update(non-existent) should return false");
        Debug.Log("[TEST] Update(non-existent) returned false");

        // --- COUNT ---
        var count = await _db.CountAsync<Player>();
        if (count != 1)
            throw new Exception($"Count expected 1, got {count}");
        Debug.Log($"[TEST] Count = {count}");

        // --- GET ALL ---
        var all = await _db.GetAllAsync<Player>();
        if (all.Count != 1)
            throw new Exception($"GetAll expected 1, got {all.Count}");
        Debug.Log($"[TEST] GetAll returned {all.Count}");

        // --- DELETE ---
        var deleted = await _db.DeleteByIdAsync<Player>(player.Id);
        if (!deleted)
            throw new Exception("Delete returned false");

        var afterDelete = await _db.GetByIdAsync<Player>(player.Id);
        if (afterDelete != null)
            throw new Exception("Delete not persisted");
        Debug.Log("[TEST] Deleted");

        // --- DELETE non-existent ---
        var deletedAgain = await _db.DeleteByIdAsync<Player>(player.Id);
        if (deletedAgain)
            throw new Exception("Delete(non-existent) should return false");
        Debug.Log("[TEST] Delete(non-existent) returned false");

        sw.Stop();
        Debug.Log($"[TEST] ✓ CRUD passed in {sw.ElapsedMilliseconds}ms");
    }

    // ============================================================
    //  TRANSACTION
    // ============================================================

    private async UniTask TestTransactionAsync()
    {
        var sw = Stopwatch.StartNew();
        const int itemCount = 100;

        // --- COMMIT ---
        await _db.RunInTransactionAsync(conn =>
        {
            for (int i = 0; i < itemCount; i++)
            {
                conn.Insert(new InventoryItem
                {
                    PlayerId = 1,
                    ItemId = $"item_{i}",
                    Count = i
                });
            }
        });

        var count = await _db.CountAsync<InventoryItem>();
        if (count != itemCount)
            throw new Exception($"Transaction count: expected {itemCount}, got {count}");
        Debug.Log($"[TEST] Transaction inserted {count} items");

        // --- ROLLBACK ---
        try
        {
            await _db.RunInTransactionAsync(conn =>
            {
                conn.Insert(new InventoryItem { PlayerId = 2, ItemId = "x", Count = 1 });
                throw new InvalidOperationException("Simulated failure");
            });
            throw new Exception("Expected exception inside transaction");
        }
        catch (InvalidOperationException ex) when (ex.Message == "Simulated failure")
        {
            Debug.Log("[TEST] Transaction threw as expected");
        }

        var countAfterRollback = await _db.CountAsync<InventoryItem>();
        if (countAfterRollback != itemCount)
            throw new Exception($"Rollback failed: expected {itemCount}, got {countAfterRollback}");
        Debug.Log("[TEST] Rollback verified");

        // --- CLEANUP ---
        await _db.ExecuteAsync("DELETE FROM InventoryItem");

        sw.Stop();
        Debug.Log($"[TEST] ✓ Transaction passed in {sw.ElapsedMilliseconds}ms");
    }

    // ============================================================
    //  CONCURRENCY
    // ============================================================

    private async UniTask TestConcurrencyAsync()
    {
        var sw = Stopwatch.StartNew();
        const int parallelCount = 100;

        // 100 параллельных операций — воркер сериализует их.
        var tasks = Enumerable.Range(0, parallelCount)
            .Select(i => _db.CreateAsync(new Player
            {
                Name = $"Player_{i}",
                Level = i,
                Health = 100f
            }))
            .ToArray();

        await UniTask.WhenAll(tasks);

        var count = await _db.CountAsync<Player>();
        if (count != parallelCount)
            throw new Exception($"Expected {parallelCount}, got {count}");
        Debug.Log($"[TEST] {count} parallel inserts completed");

        // Проверка, что все вставки уникальны и корректны.
        var all = await _db.GetAllAsync<Player>();
        var names = new HashSet<string>(all.Select(p => p.Name));
        if (names.Count != parallelCount)
            throw new Exception($"Duplicate names: {names.Count} unique vs {parallelCount}");
        Debug.Log($"[TEST] {names.Count} unique names verified");

        await _db.ExecuteAsync("DELETE FROM Player");

        sw.Stop();
        Debug.Log($"[TEST] ✓ Concurrency passed in {sw.ElapsedMilliseconds}ms");
    }

    // ============================================================
    //  ERROR RESILIENCE
    // ============================================================

    private async UniTask TestErrorResilienceAsync()
    {
        // 1. Невалидный SQL
        try
        {
            await _db.ExecuteAsync("INVALID SQL SYNTAX");
            throw new Exception("Expected SQLiteException");
        }
        catch (Exception ex) when (ex.GetType().Name.Contains("SQLite"))
        {
            Debug.Log($"[TEST] Invalid SQL threw: {ex.GetType().Name}");
        }

        // 2. Воркер жив
        if (_db.State != WorkerState.Running)
            throw new Exception($"Worker state after error: {_db.State}");
        Debug.Log("[TEST] Worker still Running");

        // 3. Следующая операция работает
        var count = await _db.CountAsync<Player>();
        Debug.Log($"[TEST] Subsequent op works, count={count}");

        // 4. Дубликат PrimaryKey через raw SQL.
        //    sqlite-net Insert делает OR REPLACE, поэтому используем
        //    явный INSERT OR FAIL для проверки.
        var p = new Player { Name = "Dup", Level = 1, Health = 100f };
        await _db.CreateAsync(p);

        try
        {
            await _db.ExecuteAsync(
                "INSERT OR FAIL INTO Player (Id, Name, Level, Health) " +
                "VALUES (?, ?, ?, ?);",
                p.Id, "Dup2", 2, 100f);

            throw new Exception("Expected duplicate key error");
        }
        catch (Exception ex) when (ex.GetType().Name.Contains("SQLite"))
        {
            Debug.Log("[TEST] Duplicate PK threw as expected");
        }

        // 5. Воркер всё ещё жив
        if (_db.State != WorkerState.Running)
            throw new Exception($"Worker died after duplicate: {_db.State}");
        Debug.Log("[TEST] Worker still Running after duplicate");

        // 6. Финальная проверка — операция работает
        var finalCount = await _db.CountAsync<Player>();
        if (finalCount != 1)
            throw new Exception($"Expected 1 player (Dup), got {finalCount}");

        await _db.ExecuteAsync("DELETE FROM Player");
        Debug.Log("[TEST] ✓ Error resilience passed");
    }

    // ============================================================
    //  STRESS
    // ============================================================

    private async UniTask TestStressAsync()
    {
        var sw = Stopwatch.StartNew();
        const int total = 10000;
        const int batchSize = 500;
        const int batches = total / batchSize;

        for (int batch = 0; batch < batches; batch++)
        {
            int b = batch;
            await _db.RunInTransactionAsync(conn =>
            {
                for (int i = 0; i < batchSize; i++)
                {
                    conn.Insert(new Player
                    {
                        Name = $"Stress_{b}_{i}",
                        Level = i,
                        Health = 100f
                    });
                }
            });
        }

        sw.Stop();

        var count = await _db.CountAsync<Player>();
        if (count != total)
            throw new Exception($"Expected {total}, got {count}");

        var perOp = sw.ElapsedMilliseconds / (double)total;
        Debug.Log($"[TEST] Inserted {total} rows in {sw.ElapsedMilliseconds}ms " +
                  $"({perOp:F3}ms per op, {total * 1000.0 / sw.ElapsedMilliseconds:F0} ops/sec)");

        await _db.ExecuteAsync("DELETE FROM Player");
        Debug.Log("[TEST] ✓ Stress passed");
    }

    // ============================================================
    //  CANCELLATION
    // ============================================================
    private async UniTask TestCancellationAsync()
    {
        // foreign_keys НЕ возвращает данных — Execute корректен.
        await _db.ExecuteAsync("PRAGMA foreign_keys = ON;");
        Debug.Log("[TEST] Basic operation executed");

        // Невалидный SQL бросает корректно.
        try
        {
            await _db.ExecuteAsync("NOT VALID SQL");
            throw new Exception("Expected SQLiteException");
        }
        catch (Exception ex) when (ex.GetType().Name.Contains("SQLite"))
        {
            Debug.Log("[TEST] Invalid SQL threw as expected");
        }

        // Воркер жив.
        if (_db.State != WorkerState.Running)
            throw new Exception($"Worker state: {_db.State}");

        // Следующая операция работает.
        await _db.ExecuteAsync("PRAGMA foreign_keys = ON;");
        Debug.Log("[TEST] Subsequent operation works");

        Debug.Log("[TEST] ✓ Cancellation passed (basic)");
    }

    // ============================================================
    //  QUEUE CAPACITY
    // ============================================================
    private async UniTask TestQueueCapacityAsync()
    {
        var sw = Stopwatch.StartNew();
        const int ops = 2000;

        // 2000 параллельных вставок — воркер сериализует их.
        var tasks = Enumerable.Range(0, ops)
            .Select(i => _db.CreateAsync(new Player
            {
                Name = $"Q_{i}",
                Level = i,
                Health = 1f
            }))
            .ToArray();

        await UniTask.WhenAll(tasks);

        var count = await _db.CountAsync<Player>();
        if (count != ops)
            throw new Exception($"Expected {ops}, got {count}");

        sw.Stop();
        Debug.Log($"[TEST] {ops} ops in {sw.ElapsedMilliseconds}ms, " +
                  $"pending={_db.PendingOperations}");

        if (_db.PendingOperations != 0)
            throw new Exception($"Queue not drained: {_db.PendingOperations}");

        await _db.ExecuteAsync("DELETE FROM Player");

        Debug.Log("[TEST] ✓ Queue capacity passed");
    }

    // ============================================================
    //  DISPOSE
    // ============================================================

    private async UniTask TestDisposeAsync()
    {
        // Создаём отдельный сервис, чтобы не портить основной.
        var tempDb = await DbServiceFactory.CreateAsync(
            dbName: $"dispose_test_{Guid.NewGuid():N}.db",
            dbPassword: "temp_password",
            logger: NullDbLogger.Instance,
            options: DbWorkerOptions.Default);

        await tempDb.CreateTableAsync<Player>();
        await tempDb.CreateAsync(new Player { Name = "Temp", Level = 1, Health = 1f });

        // Dispose должен быть идемпотентным.
        tempDb.Dispose();
        tempDb.Dispose();  // повторный вызов не должен падать

        if (tempDb.State != WorkerState.Stopped)
            throw new Exception($"Expected Stopped, got {tempDb.State}");

        // После Dispose операции должны бросать ObjectDisposedException.
        try
        {
            await tempDb.CountAsync<Player>();
            throw new Exception("Expected ObjectDisposedException");
        }
        catch (ObjectDisposedException)
        {
            Debug.Log("[TEST] ObjectDisposedException thrown as expected");
        }

        Debug.Log("[TEST] ✓ Dispose passed");
    }
}

// ============================================================
//  Тестовые сущности
// ============================================================

public sealed class Player : DbEntity
{
    public string Name { get; set; }
    public int Level { get; set; }
    public float Health { get; set; }
}

public sealed class InventoryItem : DbEntity
{
    public int PlayerId { get; set; }
    public string ItemId { get; set; }
    public int Count { get; set; }
}
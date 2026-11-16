# SqlCipherWrapper

A thread-safe, asynchronous wrapper around **SQLCipher** for Unity. All database operations run on a dedicated background thread, serialized through a queue, and exposed to the main thread as `UniTask` methods. Encryption at rest, WAL journaling, transactions, and diagnostics out of the box.

---

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Core Concepts](#core-concepts)
  - [DbEntity](#dbentity)
  - [DbWorker](#dbworker)
  - [DbService / IDbService](#dbservice--idservice)
  - [DbServiceFactory](#dbservicefactory)
  - [WorkItem / WorkItemBase](#workitem--workitembase)
  - [WorkerState](#workerstate)
- [Configuration](#configuration)
  - [DbWorkerOptions](#dbworkeroptions)
  - [IDbConnectionFactory](#idbconnectionfactory)
- [Usage](#usage)
  - [Schema](#schema)
  - [CRUD](#crud)
  - [Transactions](#transactions)
  - [Raw SQL](#raw-sql)
  - [Diagnostics](#diagnostics)
- [Logging](#logging)
- [Threading Model](#threading-model)
- [Error Handling](#error-handling)
- [Lifecycle](#lifecycle)
- [Test Harness](#test-harness)
- [Limitations](#limitations)
- [Dependencies](#dependencies)
- [License](#license)

---

## Features

- 🔐 **Encrypted at rest** — SQLCipher via `SqlCipher4Unity3D`
- 🧵 **Single-owner thread** — the `SQLiteConnection` never leaves the worker thread
- ⚡ **UniTask API** — all operations are awaitable, no callbacks, no coroutines
- 📥 **Bounded queue** — configurable capacity, backpressure on `EnqueueAsync`
- 🔁 **Serialized execution** — no SQLite-level locking contention
- 🧯 **Error isolation** — an operation failure does not kill the worker
- 📦 **Transactions** — `RunInTransactionAsync` with automatic rollback
- 🔌 **Pluggable** — logger, connection factory, and worker options
- 🧪 **Test harness** — `DbTestRunner` MonoBehaviour with 8 scenarios
- 🩺 **Diagnostics** — `WorkerState` and `PendingOperations`

---

## Requirements

| Component | Version |
|---|---|
| Unity | 2021.3 LTS or newer |
| [UniTask (Cysharp)](https://github.com/Cysharp/UniTask) | 2.x |
| [SqlCipher4Unity3D](https://github.com/Spreads/UnitySQLiteNet) or a compatible SQLCipher binding | latest |

---

## Installation

1. Install **UniTask** and your **SQLCipher** binding via UPM.
2. Clone or copy the repository into your project:
   ```bash
   git clone https://github.com/ini-dev2/WebRTCWrapper.git
   ```
3. The `SqlCipherWrapper.Runtime` assembly is picked up automatically.

---

## Quick Start

```csharp
using SqlCipherWrapper.Runtime;
using Cysharp.Threading.Tasks;

public class DbBootstrap
{
    private IDbService _db;

    public async UniTask InitAsync()
    {
        _db = await DbServiceFactory.CreateAsync(
            dbName: "game.db",
            dbPassword: "super-secret",
            logger: new UnityDbLogger(verbose: true),
            options: DbWorkerOptions.VrDefault);

        await _db.CreateTableAsync<Player>();

        var player = new Player { Name = "Alice", Level = 1, Health = 100f };
        await _db.CreateAsync(player);

        var loaded = await _db.GetByIdAsync<Player>(player.Id);
        UnityEngine.Debug.Log(loaded?.Name);

        _db.Dispose();
    }
}
```

---

## Architecture

```
┌───────────────┐       ┌───────────────┐       ┌────────────────┐
│  DbService    │──────►│  DbWorker     │──────►│  SQLite        │
│ (IDbService)  │       │  (queue loop) │       │  Connection    │
└───────────────┘       └───────┬───────┘       └────────────────┘
                                │
                                ▼
                     ┌──────────────────┐
                     │ WorkItemBase     │
                     │  WorkItem<T>     │
                     └──────────────────┘

Calling thread (Unity main) ──enqueue──► Worker thread ──execute──► SQLite
                               ◄──TaskCompletionSource── result
```

- `DbService` exposes the user-facing API and forwards operations to `DbWorker`.
- `DbWorker` owns a background thread that exclusively holds the `SQLiteConnection`.
- Each operation is a `WorkItemBase` placed into a bounded `BlockingCollection`.
- Results are returned via `TaskCompletionSource<T>` with `RunContinuationsAsynchronously`, so continuations resume on the Unity main thread.

---

## Core Concepts

### DbEntity

Base class for every persisted entity. Provides an auto-incremented integer `Id` primary key.

```csharp
public abstract class DbEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
}
```

All tables must inherit from `DbEntity` to use the generic CRUD API.

### DbWorker

Internal, thread-owning executor. Not part of the public API — use `DbService` or `DbServiceFactory` instead.

Responsibilities:

- Starts a dedicated `Thread` (`ThreadName` configurable).
- Creates the `SQLiteConnection` **inside** the worker thread (never leaks).
- Configures SQLite `PRAGMA`s: `journal_mode=WAL`, `synchronous=NORMAL`, `foreign_keys=ON`, `cache_size=-8000`, `busy_timeout=5000`, `temp_store=MEMORY`.
- Consumes `WorkItemBase` from a `BlockingCollection`, executing them one at a time.
- On fatal error, drains the queue and fails all pending items.

Key members:

| Member | Description |
|---|---|
| `State` | Current `WorkerState` |
| `IsRunning` | `State == Running` |
| `QueueCount` | Number of pending items |
| `StartAsync(factory, ct)` | Starts the thread and waits until `Running` (or `FatalError`) |
| `EnqueueAsync<T>(op, ct, desc)` | Enqueues an operation returning `T` |
| `EnqueueVoidAsync(op, ct, desc)` | Enqueues a void operation |
| `Dispose()` | Completes the queue, cancels the thread, waits up to `ShutdownTimeout` |

### DbService / IDbService

The public façade consumed by application code.

```csharp
public interface IDbService : IDisposable
{
    // Schema
    UniTask CreateTableAsync<TEntity>() where TEntity : DbEntity;
    UniTask DropTableAsync<TEntity>() where TEntity : DbEntity;

    // CRUD
    UniTask CreateAsync<TEntity>(TEntity data) where TEntity : DbEntity;
    UniTask<TEntity> GetByIdAsync<TEntity>(int id)
        where TEntity : DbEntity, new();
    UniTask<IReadOnlyList<TEntity>> GetAllAsync<TEntity>()
        where TEntity : DbEntity, new();
    UniTask<bool> UpdateAsync<TEntity>(TEntity data) where TEntity : DbEntity;
    UniTask<bool> DeleteByIdAsync<TEntity>(int id) where TEntity : DbEntity;
    UniTask<int> CountAsync<TEntity>() where TEntity : DbEntity, new();

    // Batch
    UniTask RunInTransactionAsync(Action<SQLiteConnection> operations);

    // Raw SQL
    UniTask<int> ExecuteAsync(string sql, params object[] args);
    UniTask<List<T>> QueryAsync<T>(string sql, params object[] args)
        where T : new();

    // Diagnostics
    WorkerState State { get; }
    int PendingOperations { get; }
}
```

`DbService` is the concrete implementation. `Dispose()` is idempotent — subsequent calls are no-ops.

### DbServiceFactory

Encapsulates async initialization so callers cannot forget to start the worker.

```csharp
public static UniTask<DbService> CreateAsync(
    string dbName,
    string dbPassword,
    IDbLogger logger = null,
    DbWorkerOptions options = null,
    IDbConnectionFactory connectionFactory = null,
    CancellationToken ct = default);
```

- Resolves the path to `Application.persistentDataPath/<dbName>`.
- Creates the parent directory if missing.
- Instantiates a `DbWorker` and calls `StartAsync`.
- On failure, disposes the worker and rethrows.

### WorkItem / WorkItemBase

`WorkItemBase` is the non-generic base stored in the `BlockingCollection` (which requires a single element type). `WorkItem<T>` wraps a `Func<SQLiteConnection, T>` and a `TaskCompletionSource<T>`.

**Critical detail:** `TaskCompletionSource<T>` is created with `TaskCreationOptions.RunContinuationsAsynchronously`. Without this flag, awaits would resume in the worker thread and break Unity APIs (which must be used on the main thread).

Cancellation: `CancellationToken.Register` fires `TrySetCanceled`. The operation itself does not abort (SQLite does not support it from C#), but the caller stops waiting.

### WorkerState

```csharp
public enum WorkerState
{
    Created,      // StartAsync not called
    Starting,     // Thread started, opening connection
    Running,      // Ready to accept operations
    Stopping,     // Dispose called, queue draining
    Stopped,      // Thread finished
    FatalError    // Critical error; worker unusable
}
```

---

## Configuration

### DbWorkerOptions

| Property | Default | Description |
|---|---|---|
| `ThreadName` | `"SqlCipher.DbWorker"` | Visible in debuggers and crash reports |
| `ThreadPriority` | `BelowNormal` | Avoids competing with VR rendering |
| `QueueCapacity` | `1024` | Max queue length; `0` = unbounded (dangerous) |
| `ShutdownTimeout` | `5s` | Max graceful shutdown at `Dispose` |
| `OperationTimeout` | `0` (off) | Currently only logs, SQLite cannot abort |
| `VerboseLogging` | `false` | Logs every operation |
| `AutoRestartOnFatalError` | `false` | Not implemented |
| `MaxRestartAttempts` | `3` | Not implemented |

Presets:

- `DbWorkerOptions.Default` — general purpose.
- `DbWorkerOptions.VrDefault` — `BelowNormal` priority, small queue, `VerboseLogging = false`.
- `DbWorkerOptions.Editor` — `Normal` priority, large queue, `VerboseLogging = true`.

### IDbConnectionFactory

```csharp
public interface IDbConnectionFactory
{
    SQLiteConnection Create(string path, string password);
}
```

`DefaultConnectionFactory` creates a `SQLCipher` connection via `SqlCipher4Unity3D`. In tests, inject an in-memory factory (e.g., `:memory:`).

---

## Usage

### Schema

```csharp
await _db.CreateTableAsync<Player>();
await _db.CreateTableAsync<InventoryItem>();
// later…
await _db.DropTableAsync<Player>();
```

`CreateTable<T>` is idempotent (uses `CREATE TABLE IF NOT EXISTS` under the hood via `sqlite-net`).

### CRUD

```csharp
var player = new Player { Name = "Alice", Level = 1, Health = 100f };
await _db.CreateAsync(player);              // sets player.Id

var loaded = await _db.GetByIdAsync<Player>(player.Id);  // null if missing

loaded.Level = 10;
bool updated = await _db.UpdateAsync(loaded);            // false if not found

var all = await _db.GetAllAsync<Player>();               // IReadOnlyList<Player>
int count = await _db.CountAsync<Player>();

bool deleted = await _db.DeleteByIdAsync<Player>(player.Id);  // false if not found
```

### Transactions

```csharp
await _db.RunInTransactionAsync(conn =>
{
    for (int i = 0; i < 100; i++)
        conn.Insert(new InventoryItem { PlayerId = 1, ItemId = $"item_{i}", Count = i });
});
```

Wraps `BeginTransaction` / `Commit`, rolls back on any exception. Atomic; only the last operation fails on the whole batch otherwise.

### Raw SQL

```csharp
int affected = await _db.ExecuteAsync("DELETE FROM Player WHERE Level < ?", 5);

var rows = await _db.QueryAsync<Player>("SELECT * FROM Player WHERE Level > ?", 5);
```

Use `Execute` for `PRAGMA` and DML; use `Query` for `SELECT`. `Execute` correctly handles statements that return `SQLITE_DONE` (no rows).

### Diagnostics

```csharp
WorkerState state = _db.State;              // Running
int pending = _db.PendingOperations;        // queued-but-not-yet-executed
```

---

## Logging

`IDbLogger` is a tiny abstraction:

```csharp
public interface IDbLogger
{
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception ex = null);
}
```

| Implementation | Notes |
|---|---|
| `UnityDbLogger(verbose)` | `Info` gated by `verbose`; `Warning`/`Error` always go through `Debug.*` |
| `NullDbLogger.Instance` | No-op singleton |

`DbWorkerOptions.VerboseLogging` additionally controls per-operation logging inside the worker (enqueue, completion time, PRAGMA application).

---

## Threading Model

- **Caller thread:** builds the work item, enqueues it, awaits the returned `UniTask`. Continuations resume on the Unity main thread thanks to `RunContinuationsAsynchronously`.
- **Worker thread:** owns the `SQLiteConnection` for its entire lifetime. Never leaks — even `Dispose` is performed inside the loop's `finally`.
- **Serialization:** every operation runs to completion before the next one starts. No concurrent SQLite access, no retries on `SQLITE_BUSY` needed beyond `busy_timeout`.
- **Cancellation:** the operation is marked canceled on the caller side; the worker still finishes it (SQLite lacks an abort mechanism), but no one waits.

The `BlockingCollection` uses `ConcurrentQueue<WorkItemBase>` with `QueueCapacity` as the bound. When full, `EnqueueAsync` blocks until space is available — that's the intended backpressure mechanism.

---

## Error Handling

| Situation | Behavior |
|---|---|
| Operation throws | `_tcs.TrySetException(ex)`, worker keeps running |
| Worker loop throws | `_fatalError` set, state → `FatalError`, pending items failed |
| `Dispose` during long op | Waits up to `ShutdownTimeout`, then logs and abandons the thread |
| Enqueue after dispose | `ObjectDisposedException` |
| Enqueue in `FatalError` | `InvalidOperationException` wrapping the original error |
| Caller cancels | `OperationCanceledException` (`TrySetCanceled`) |

Fatal errors are the only case where the worker stops accepting work. All operation-level errors are isolated.

---

## Lifecycle

```
Created ──StartAsync──► Starting ──connection ready──► Running
                                                            │
                                                            │ Dispose()
                                                            ▼
                                                        Stopping
                                                            │ thread join
                                                            ▼
                                                        Stopped

OnStartAsync/loop exception: ───────────────────────► FatalError
```

- `StartAsync` waits up to 30 seconds for `Running`; throws `TimeoutException` otherwise.
- `Dispose` is idempotent and safe to call from any thread.
- After `Dispose`, further operations throw `ObjectDisposedException`.

---

## Test Harness

`DbTestRunner` is a `MonoBehaviour` with a **ContextMenu** exposing 10 scenarios:

| Menu item | Scenario |
|---|---|
| `0. Run All Tests` | Runs everything below |
| `1. Init DB` | `DbServiceFactory.CreateAsync` + `CreateTable` |
| `2. CRUD Test` | Insert, read, update, delete, and non-existent cases |
| `3. Transaction Test` | Commit and rollback |
| `4. Concurrency Test` | 100 parallel inserts |
| `5. Error Resilience Test` | Invalid SQL, duplicate PK, worker survival |
| `6. Stress Test (10000 ops)` | 10 000 inserts in 20 transaction batches |
| `7. Cancellation Test` | Basic — SQLite cannot abort mid-operation |
| `8. Queue Capacity Test` | 2 000 queued inserts, checks drain |
| `9. Dispose Test` | Isolated service; double dispose; `ObjectDisposedException` |
| `99. Dispose DB` | Manual teardown |

Run it from the inspector while in Play Mode. The `Start` method waits 7 seconds before running so you can observe editor startup.

> **Note:** the harness targets a real device path via `Application.persistentDataPath`. In the editor, the DB file lives under `%AppData%/LocalLow/<Company>/<Product>/`.

---

## Limitations

- **No mid-operation abort.** `CancellationToken` only stops the caller from waiting; the SQLite call runs to completion. `OperationTimeout` is therefore a placeholder.
- **`AutoRestartOnFatalError` and `MaxRestartAttempts` are declared but not implemented.** A `FatalError` requires a new `DbWorker`.
- **`DbWorker` is `internal`.** You cannot subclass or replace it; use `DbServiceFactory` with a custom `IDbConnectionFactory` and `DbWorkerOptions`.
- **No migrations.** `CreateTable<T>` is `IF NOT EXISTS`-style; schema changes require `DropTableAsync` or manual SQL.
- **Transactions use sqlite-net's `BeginTransaction`.** Nested transactions are not supported; use a single `RunInTransactionAsync`.
- **`QueryAsync<T>` requires `new()`** because sqlite-net materializes instances via `Activator.CreateInstance`.
- **`StringState`-like constructor gating is not present here.** `DbService` has an `internal` constructor — the only supported creation path is `DbServiceFactory`.
- **One worker = one connection.** Opening multiple databases means multiple `DbService` instances, each with its own thread. There is no shared pool.

---

## Dependencies

- [UniTask](https://github.com/Cysharp/UniTask) — `UniTask`, `UniTask.Delay`, `UniTask.WhenAll`
- [SqlCipher4Unity3D](https://github.com/Spreads/UnitySQLiteNet) — `SQLiteConnection` and SQLCipher binding
- [sqlite-net](https://github.com/praeclarum/sqlite-net) — attribute-based ORM (`[PrimaryKey]`, `[AutoIncrement]`, `Table<T>()`, `Insert`, `Update`, `Delete`)

---

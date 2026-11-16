# Tickable

A lightweight, allocation-conscious tick system for Unity. Register any `IUpdateTick` into one of three tick groups — **every frame**, **fixed tick rate**, or **custom interval** — and drive them from a single `PlayerLoop` hook instead of spreading `Update()` across dozens of MonoBehaviours.

---

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Core Concepts](#core-concepts)
  - [ITick / IUpdateTick](#itick--iupdatetick)
  - [TickType](#ticktype)
  - [UpdateType](#updatetype)
  - [ITickManager\<T\>](#itickmanagert)
  - [UpdateTickManager](#updatetickmanager)
- [Usage](#usage)
  - [Register & Remove](#register--remove)
  - [Play / Stop / CallHook](#play--stop--callhook)
  - [Custom tick rate](#custom-tick-rate)
  - [Custom intervals](#custom-intervals)
- [PlayerLoop Integration](#playerloop-integration)
- [Lifecycle](#lifecycle)
- [Limitations](#limitations)
- [Dependencies](#dependencies)
- [License](#license)

---

## Features

- 🎯 **Three tick modes** — every frame (`NONE`), fixed rate (`ON_TICKRATE`), custom interval (`ON_SECONDS`)
- 🧵 **Single PlayerLoop hook** — no `MonoBehaviour.Update()` sprawl
- 🧱 **Decoupled** — tick objects do not need to inherit from `MonoBehaviour`
- 🧊 **Exception isolation** — a throwing tick does not stop others
- 📦 **HashSet-based** — no duplicate registrations, O(1) add/remove
- 🗂️ **Cached interval groups** — rebuilt only when the interval set changes
- ⏯️ **Play / Stop** — pause without unregistering
- 🧹 **Idempotent Dispose** — safe to call multiple times

---

## Requirements

| Component | Version |
|---|---|
| Unity | 2020.1+ (uses `PlayerLoop.GetCurrentPlayerLoop` / `SetPlayerLoop`) |
| C# | 8.0+ (`Array.Empty`, index-from-end `^1`, using declarations) |

No external packages required.

---

## Installation

1. Clone or copy the repository into your project:
   ```bash
   git clone https://github.com/ini-dev2/WebRTCWrapper.git
   ```
2. The `Tickable.Runtime` assembly is picked up automatically.

---

## Quick Start

```csharp
using UnityEngine;
using Tickable.Runtime;

public sealed class Player : IUpdateTick
{
    private float _speed = 5f;

    public void Tick(float dTime)
    {
        // called every frame by the manager
    }
}

public sealed class Bootstrap : MonoBehaviour
{
    private UpdateTickManager _manager;
    private Player _player;

    private void Awake()
    {
        _manager = new UpdateTickManager(tickRate: 64);

        _player = new Player();

        // every frame
        _manager.Register(TickType.NONE, _player);

        // install the PlayerLoop hook
        _manager.CallHook();
    }

    private void OnDestroy()
    {
        _manager.Dispose();
    }
}
```

---

## Architecture

```
┌───────────────────────┐
│  UpdateTickManager    │
│                       │
│  _frameTicks   : Set  │◄── TickType.NONE
│  _rateTicks    : Set  │◄── TickType.ON_TICKRATE
│  _secondsTicks : Dict │◄── TickType.ON_SECONDS  ─┐
│      │                                          │
│      ▼                                          │
│  _secondsTickCache[]  (rebuilt on dirty) ◄──────┘
└───────────┬───────────┘
            │ installed into
            ▼
   Unity PlayerLoop
   (Update sub-system)
```

`UpdateTickManager` injects a single `PlayerLoopSystem` into Unity's `Update` phase. Every frame the injected delegate calls `Update(Time.deltaTime)`, which dispatches to each registered group.

---

## Core Concepts

### ITick / IUpdateTick

```csharp
public interface ITick
{
    void Tick(float dTime);
}

public interface IUpdateTick : ITick { }
```

`IUpdateTick` is the concrete marker used by `UpdateTickManager`. `ITick` exists so other managers (e.g., `FixedTickManager`) can share a common contract.

### TickType

```csharp
public enum TickType
{
    NONE       = 0,  // every frame
    ON_TICKRATE = 1, // fixed tick rate (constructor arg)
    ON_SECONDS  = 2  // custom interval, passed on Register
}
```

`ON_SECONDS` **must** be registered with an interval — calling the two-argument `Register` with `ON_SECONDS` throws `ArgumentException`.

### UpdateType

```csharp
public enum UpdateType
{
    UPDATE       = 1,
    FIXED_UPDATE = 2,
    LATE_UPDATE  = 3,
}
```

Reserved for future managers. `UpdateTickManager` currently installs into `Update` only.

### ITickManager<T>

```csharp
public interface ITickManager<T> : IDisposable where T : ITick
{
    void CallHook();
    void PlayHook();
    void StopHook();

    void Register(TickType type, T tick);
    void Register(TickType type, T tick, float intervalSeconds);

    void Remove(TickType type, T tick);
    void Remove(TickType type, T tick, float intervalSeconds);
}
```

The contract used by `UpdateTickManager`. Overloads without an interval are valid for `NONE` and `ON_TICKRATE`; overloads with an interval are valid for `ON_SECONDS`.

### UpdateTickManager

The concrete implementation.

- **Constructor:** `UpdateTickManager()` defaults to **64 Hz**; `UpdateTickManager(int tickRate)` validates `tickRate > 0`.
- **Internal storage:** `HashSet<IUpdateTick>` for frame and rate groups; `Dictionary<TimeSpan, SecondsTickGroup<IUpdateTick>>` for intervals.
- **Cache:** an array of interval groups rebuilt lazily when the dictionary changes (`_secondsTickCacheDirty`). This avoids dictionary enumeration per-frame in `ExecuteTicks`.
- **Dispatch:** `Update(float dTime)` handles all three groups, guarding against `dTime < 0`.
- **Exception handling:** `ExecuteTicks` snapshots the `HashSet` into an array, iterates it, and wraps each `Tick` in `try/catch` + `Debug.LogException`. One misbehaving tick does not break the loop.

---

## Usage

### Register & Remove

```csharp
var manager = new UpdateTickManager(tickRate: 64);
var tick = new MyTick();

// every frame
manager.Register(TickType.NONE, tick);

// fixed 64 Hz
manager.Register(TickType.ON_TICKRATE, tick);

// custom interval
manager.Register(TickType.ON_SECONDS, tick, intervalSeconds: 0.25f);

// remove
manager.Remove(TickType.NONE, tick);
manager.Remove(TickType.ON_SECONDS, tick, intervalSeconds: 0.25f);
```

`Register` logs `Warning` on duplicate adds; `Remove` logs `Warning` when the tick is not found. Both ignore `null` with `Debug.LogError`.

### Play / Stop / CallHook

```csharp
manager.CallHook();   // installs the PlayerLoop system, starts ticking
manager.StopHook();   // pauses dispatch (registered ticks stay registered)
manager.PlayHook();   // resumes
```

`CallHook` throws `InvalidOperationException` if Unity's `Update` system is not present in the current `PlayerLoopSystem` tree (e.g., on platforms with a stripped player loop).

`StopHook` / `PlayHook` both throw `ObjectDisposedException` after `Dispose`.

### Custom tick rate

```csharp
var manager = new UpdateTickManager(tickRate: 128);
```

Accumulator-based: `tickAccumulator += dTime`; while it exceeds `1f / tickRate`, the rate group is ticked with the exact step (`_tickInterval`) so fixed-step logic sees consistent `dTime`.

### Custom intervals

```csharp
manager.Register(TickType.ON_SECONDS, slowTick, intervalSeconds: 1f);
manager.Register(TickType.ON_SECONDS, fastTick, intervalSeconds: 0.1f);
```

Each `TimeSpan` key owns an independent accumulator. The `SecondsTickGroup` cache is rebuilt only when the set of intervals changes.

---

## PlayerLoop Integration

`CallHook()` appends a custom `PlayerLoopSystem` into the built-in `Update` sub-system:

```csharp
PlayerLoopSystem customSystem = new PlayerLoopSystem
{
    type = typeof(UpdateTickManager),
    updateDelegate = OnPlayerLoopUpdate
};
```

`AppendToSystem` recursively walks `parent.subSystemList` looking for `typeof(Update)` and appends. `RemoveSystem` filters `PlayerLoopSystem` entries whose `type == typeof(UpdateTickManager)` and recurses into children.

> ⚠️ The hook is **global**: only one `UpdateTickManager` should call `CallHook()` at a time. `Dispose()` removes the hook before tearing down collections.

---

## Lifecycle

```
Created ──CallHook──► isPlaying = true
                          │
                          ├── StopHook  ──► isPlaying = false
                          ├── PlayHook  ──► isPlaying = true
                          ▼
                      Dispose()
                          │
                          ├── isPlaying = false
                          ├── disposed = true
                          ├── DisposeHook()  (removes from PlayerLoop)
                          ├── clears all collections
                          └── resets accumulator
```

After `Dispose`:

- `Register`, `Remove`, `PlayHook`, `StopHook` throw `ObjectDisposedException`.
- `Dispose` is a no-op.

---

## Limitations

- **One active hook per manager type.** `AppendToSystem` matches on `typeof(Update)`. Multiple managers will install multiple identical delegates; only the last installed loop remains functional in some Unity versions. Track a single manager per game.
- **No fixed-rate `FIXED_UPDATE` / `LATE_UPDATE`.** `UpdateType` is declared but not wired — `UpdateTickManager` installs only into the `Update` phase.
- **`ON_TICKRATE` uses `dTime` accumulator, not real time.** After a long frame, multiple rate ticks fire in one frame (with `_tickInterval` step). This is intentional but visible in profiling.
- **No buffer system.** The source has `//TODO: Buffer system` — every `ExecuteTicks` allocates a new `IUpdateTick[]` snapshot. This is fine for moderate counts but allocates per tick.
- **`Remove` on an already-removed tick** logs `Warning` instead of throwing.
- **No priority ordering.** `HashSet` iteration order is not guaranteed; ticks cannot express "run before/after".
- **No per-tick exception filtering.** Every exception is caught and logged via `Debug.LogException`, which is expensive in Release builds.
- **`UpdateType` is unused.** Remove or wire it before relying on it.
- **`CallHook` must be called from the main thread.** It manipulates `PlayerLoop` and is not thread-safe.

---

## Dependencies

- `UnityEngine.LowLevel` — `PlayerLoopSystem`, `PlayerLoop`
- `UnityEngine.PlayerLoop` — `Update` phase
- `UnityEngine.Debug` — error / warning logging
- `System.Collections.Generic` — `HashSet`, `Dictionary`, `List`

No NuGet, no UniTask, no Newtonsoft. Pure C# + Unity.

---

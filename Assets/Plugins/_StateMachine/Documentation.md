# UniStateMachine

A graph-based, data-driven state machine for Unity. Build state graphs visually with **Unity GraphToolkit**, then import them at runtime as `ScriptableObject` assets — no boilerplate code required.

---

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Graph Preview](#graph-preview)
- [Editor Components](#editor-components)
  - [StateMachineGraph](#statemachinegraph)
  - [Nodes](#nodes)
  - [GraphRuntimeImporter](#graphruntimeimporter)
- [Runtime Components](#runtime-components)
  - [IState / IState<T>](#istate--istatet)
  - [IStateMachine<T> / StateMachine<T>](#istatemachinet--statemachinet)
  - [StateMachineDataDriven](#statemachinedatadriven)
  - [StringState](#stringstate)
  - [StringKeyStateMachine](#stringkeystatemachine)
- [Usage](#usage)
- [Limitations](#limitations)
- [Dependencies](#dependencies)
- [License](#license)

---

## Features

- 🎨 **Visual graph editor** — build state machines with a node-based graph powered by Unity GraphToolkit
- 📦 **ScriptableObject output** — graphs are imported into runtime `StringKeyStateMachine` assets automatically
- 🔤 **String-keyed states** — each state is identified by a string; each state may point to the next one
- 🧬 **Generic core** — `StateMachine<T>` works with any key type (`Enum`, `string`, custom types)
- 🎯 **Editor validation** — duplicate IDs, unreachable nodes, and dangling references are reported on import
- 🔌 **Event-driven** — `IState.OnEnter` / `IState.OnExit` events for hooking behavior

---

## Requirements

| Component | Version |
|---|---|
| Unity | 6.0 or newer (GraphToolkit is required) |
| [Unity GraphToolkit](https://docs.unity3d.com/Packages/com.unity.graphtoolkit@latest) | latest |

> GraphToolkit is currently an experimental package. Make sure it's installed in your project before importing UniStateMachine.

---

## Installation

1. Install **Unity GraphToolkit** via the Package Manager (add by name: `com.unity.graphtoolkit`).
2. Clone or copy the repository into your project:
   ```bash
   git clone https://github.com/ini-dev2/WebRTCWrapper.git
   ```
3. The editor and runtime assemblies (`UniStateMachine.Editor`, `UniStateMachine.Runtime`) will be picked up automatically.

---

## Quick Start

1. Open **Tools → KolaTech → StateMachineGraph** in the Unity menu bar.
2. Save the new `*.StateGraph` asset.
3. Open the graph and add nodes:
   - One **StartNode** (entry point).
   - One or more **StateNode<TStateId>** nodes (choose your enum type as `TStateId`).
4. Connect the execution ports with arrows.
5. On save, the `GraphRuntimeImporter` produces a `StringKeyStateMachine` `ScriptableObject` next to the graph asset.
6. Use the resulting asset at runtime:

```csharp
using UniStateMachine.Runtime;

public class GameController
{
    private StringKeyStateMachine _stateMachine;

    public void Init(StringKeyStateMachine asset)
    {
        _stateMachine = asset;
        _stateMachine.Start();          // register all states
        _stateMachine.SetState("Idle"); // enter initial state
    }

    public void Tick()
    {
        // transition to the next state
        _stateMachine.SetState("Running");
    }
}
```

---

## Architecture

```
Editor                                       Runtime
┌────────────────────────┐                   ┌───────────────────────────┐
│ StateMachineGraph      │                   │ IStateMachine<T>          │
│  (GraphToolkit asset)  │                   │   └─ StateMachine<T>      │
│                        │                   │        └─ StateMachineDataDriven
│ StartNode ─► StateNode │                   │             └─ StringKeyStateMachine
│              │         │                   │                   │
│              ▼         │  GraphRuntime     │                   ▼
│          StateNode     │──Importer───────► │              StringState[]
│                        │  (ScriptableObj)  │
└────────────────────────┘                   └───────────────────────────┘
```

---

## Graph Preview

Here's what the graph editor looks like when you build a simple state machine:

![State machine graph preview]([Docs/Images/graph-preview.png](https://github.com/ini-dev2/Tools/blob/main/Assets/Plugins/_StateMachine/Images/Preview.jpg))

*Example: a `StartNode` connected to `Idle`, which chains to `Running` and `Stopped`. Each state node exposes a `CurrentState` enum option and input/output execution ports.*

> **To add your own screenshot:** place the image under `Docs/Images/` in the repository and reference it with a relative path as shown above. GitHub renders relative image paths in README files. You can also drag-and-drop an image into the GitHub web editor to get an auto-generated URL.

**Example layout of a graph asset:**

```
┌───────────┐     ┌───────────┐     ┌───────────┐     ┌───────────┐
│ StartNode │────►│  Idle     │────►│  Running  │────►│  Stopped  │
└───────────┘     └───────────┘     └───────────┘     └───────────┘
                   [Idle]            [Running]          [Stopped]
```

---

## Editor Components

### StateMachineGraph

The graph asset definition. It registers a custom extension (`*.StateGraph`) and a menu item to create new assets.

```csharp
[Graph(AssetExtension)]
[Serializable]
internal sealed class StateMachineGraph : Graph
{
    public const string AssetExtension = "StateGraph";

    [MenuItem("Tools/KolaTech/StateMachineGraph", priority = 100)]
    static void CreateAssetFile()
    {
        GraphDatabase.PromptInProjectBrowserToCreateNewAsset<StateMachineGraph>();
    }
}
```

### Nodes

All nodes derive from `BaseNode`, which provides helper methods for execution ports.

| Node | Purpose |
|---|---|
| `BaseNode` | Base class. Defines an input and output execution port (`ExecutionPort`). |
| `StartNode` | Graph entry point. Has only an output execution port. |
| `StateNodeBase` | Abstract base for state nodes. Requires `TryGetStateId(out Enum)`. |
| `StateNode<TStateId>` | Concrete state node. Has an enum option `CurrentState` of type `TStateId`. |

`StateNode<TStateId>` example:

```csharp
[Serializable]
public abstract class StateNode<TStateId> : StateNodeBase where TStateId : Enum
{
    public static readonly string StateFieldName = "CurrentState";

    protected override void OnDefinePorts(IPortDefinitionContext context)
        => AddInputOutputExecutionPorts(context, outputName: "Next State");

    protected override void OnDefineOptions(IOptionDefinitionContext context)
    {
        context.AddOption<TStateId>(StateFieldName)
            .WithDisplayName(StateFieldName)
            .Build();
    }

    public override bool TryGetStateId(out Enum stateId)
    {
        var option = GetNodeOptionByName(StateFieldName);
        if (option.TryGetValue<TStateId>(out var id)) { stateId = id; return true; }
        stateId = default;
        return false;
    }
}
```

### GraphRuntimeImporter

A `ScriptedImporter` that runs whenever a `*.StateGraph` asset is imported/reimported. It:

1. Loads the graph via `GraphDatabase.LoadGraphForImporter<StateMachineGraph>`.
2. Finds the `StartNode`.
3. Performs a BFS across connected state nodes.
4. Extracts each node's state enum value into a `string` ID.
5. Builds a `StringState[]` and assigns it to a fresh `StringKeyStateMachine` `ScriptableObject`.
6. Reports errors for:
   - Missing `StartNode`.
   - Duplicate state IDs.
   - State nodes without enum values.
   - Unreachable / non-existent next-state references.
   - Multiple outgoing branches (only the first is used).
7. `ctx.AddObjectToAsset` + `ctx.SetMainObject` produce the runtime asset.

---

## Runtime Components

### IState / IState<T>

```csharp
public interface IState : IDisposable
{
    event Action OnEnter;
    event Action OnExit;
}

public interface IState<T> : IState
{
    T Id { get; }
    T NextId { get; }

    void Enter();
    void Exit();
}
```

Each state has an identifier, an optional next-state ID, and enter/exit events.

### IStateMachine<T> / StateMachine<T>

```csharp
public interface IStateMachine<T> : IDisposable
{
    void SetState(T stateId);

    void Register(T stateId, IState<T> state);
    void Register(IState<T> state);

    void Remove(T stateId);
    void Remove(IState<T> state);

    IState TryGetState(T stateId);
}
```

`StateMachine<T>` is the concrete implementation:

- `Register(...)` — adds a state; throws on duplicate IDs.
- `Remove(...)` — removes a state; throws if the state is currently active.
- `SetState(id)` — calls `Exit()` on the current state, `Enter()` on the next state. Re-entering the same state is a no-op.
- `Dispose()` — disposes all states and clears the registry.

### StateMachineDataDriven

`StateMachineDataDriven` is a `ScriptableObject` wrapper around `StateMachine<string>`. It exposes the same interface and can be serialized in the inspector.

### StringState

A concrete `IState<string>` implementation with `[SerializeField]` fields for `Id` and `NextId`. The constructors are guarded by `#if UNITY_EDITOR` because they only exist for the importer.

### StringKeyStateMachine

The concrete, serializable asset produced by the importer:

```csharp
public sealed class StringKeyStateMachine : StateMachineDataDriven
{
    [SerializeField] private StringState[] _states;

    public void Start()
    {
        foreach (var item in _states)
            Register(item);
    }

#if UNITY_EDITOR
    [NonSerialized] public List<StringState> editorStates = new();

    public void Build()
        => _states = editorStates.ToArray();
#endif
}
```

`Start()` registers every state. `Build()` (editor only) copies the `editorStates` list into the serialized array.

---

## Usage

### Editor

1. **Create a graph:** `Tools → KolaTech → StateMachineGraph`.
2. **Add a StartNode** and at least one `StateNode<TStateId>` (choose an enum).
3. **Connect** the output port of the StartNode to a StateNode, then chain StateNodes together.
4. **Set the state ID** for each StateNode via the `CurrentState` option.
5. **Save** — the importer generates a `StringKeyStateMachine` asset next to the graph.

### Runtime

```csharp
using UnityEngine;
using UniStateMachine.Runtime;

public class GameRoot : MonoBehaviour
{
    [SerializeField] private StringKeyStateMachine _stateMachine;

    private void Start()
    {
        _stateMachine.Start();          // registers all states
        _stateMachine.SetState("Idle"); // enter first state
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            _stateMachine.SetState("Running");
    }

    private void OnDestroy()
    {
        _stateMachine.Dispose();
    }
}
```

### Enums and `TStateId`

`StateNode<TStateId>` is generic and requires a non-empty enum type. In the graph editor you'll need a concrete subclass or an enum option type for each state machine. The importer converts the enum value to its string representation, so the runtime asset always uses `string` keys.

---

## Limitations

- **Linear chains only.** The importer currently follows the **first** outgoing branch of a node. If a node has multiple outgoing edges, a warning is logged and only the first is used. Branching/conditions are not yet supported.
- **String keys only at runtime.** `StateNode<TStateId>` accepts any `Enum`, but the importer converts to strings and produces `StringState` / `StringKeyStateMachine`. A generic importer is not yet provided.
- **Editor constructors.** `StringState` has its constructors gated by `#if UNITY_EDITOR`, meaning you cannot create states programmatically in a player build.
- **No transition conditions.** States have no guards, conditions, or parameters — transitions are just next-state pointers.
- **`GetNextId` is a stub.** The importer calls a static `GetNextId(StringState) => null`, so the dangling-reference check in the current state of the code is effectively disabled.
- **GraphToolkit is experimental.** API may change between Unity versions.

---

## Dependencies

- [Unity GraphToolkit](https://docs.unity3d.com/Packages/com.unity.graphtoolkit@latest) — visual graph editor
- Unity 6.0+ — required by GraphToolkit

---

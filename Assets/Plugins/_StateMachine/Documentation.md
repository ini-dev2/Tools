# State Machine (Utils)

Небольшая обобщённая реализация конечного автомата (state machine) на C#, основанная на интерфейсах `IState<T>` и `IStateMachine<T>`. Состояния идентифицируются значением перечисления (`enum`), что обеспечивает типобезопасность и удобство использования.

## Содержание

- [Обзор](#обзор)
- [Требования](#требования)
- [Архитектура](#архитектура)
  - [IState&lt;T&gt;](#istatet)
  - [IStateMachine&lt;T&gt;](#istatemachinet)
  - [StateMachine&lt;T&gt;](#statemachinet)
- [Жизненный цикл состояний](#жизненный-цикл-состояний)
- [Пример использования](#пример-использования)
- [Обработка ошибок](#обработка-ошибок)
- [Ограничения и замечания](#ограничения-и-замечания)

---

## Обзор

`StateMachine<T>` — это контейнер состояний, который:

- хранит состояния в словаре `Dictionary<T, IState<T>>`;
- позволяет регистрировать и удалять состояния;
- переключается между состояниями, вызывая `Exit()` у текущего и `Enter()` у нового;
- запрещает удаление активного состояния;
- игнорирует повторный переход в уже активное состояние.

Класс не является потокобезопасным — предполагается использование в одном потоке (например, в игровом цикле или UI-потоке).

---

## Требования

- .NET с поддержкой C# 8+ (используются `target-typed new`, `where T : Enum`, `is null`-подобные проверки).
- Пространство имён: `Utils`.

---

## Архитектура

### IState&lt;T&gt;

```csharp
public interface IState<T> where T : Enum
{
    public T Id { get; }
    void Enter();
    void Exit();
}
```

Контракт состояния:

| Член | Назначение |
|------|------------|
| `Id` | Уникальный идентификатор состояния (значение `enum`). |
| `Enter()` | Вызывается при входе в состояние. |
| `Exit()` | Вызывается при выходе из состояния. |

### IStateMachine&lt;T&gt;

```csharp
public interface IStateMachine<T> where T : Enum
{
    void SetState(T stateId);
    void Register(T stateId, IState<T> state);
    void Remove(T stateId);
}
```

Контракт автомата:

| Метод | Назначение |
|-------|------------|
| `SetState(T)` | Переключает автомат в состояние с указанным идентификатором. |
| `Register(T, IState<T>)` | Регистрирует состояние под указанным идентификатором. |
| `Remove(T)` | Удаляет состояние из автомата. |

### StateMachine&lt;T&gt;

Реализация `IStateMachine<T>`.

#### Поля и свойства

| Член | Тип | Описание |
|------|-----|----------|
| `_states` | `Dictionary<T, IState<T>>` | Хранилище зарегистрированных состояний. |
| `_currentStateId` | `T` | Идентификатор активного состояния. |
| `_hasCurrentState` | `bool` | Флаг, указывающий, было ли установлено хотя бы одно состояние. |
| `CurrentState` | `IState<T>` (protected) | Доступ к активному состоянию (для наследников). |

#### Конструкторы

```csharp
public StateMachine();
public StateMachine(IEnumerable<IState<T>> states);
```

- Пустой конструктор — создаёт автомат без состояний.
- Конструктор с коллекцией — регистрирует все переданные состояния, используя их `Id`.

#### Методы

**`Register(T stateId, IState<T> state)`**

- Проверяет `stateId` и `state` на `null`.
- Бросает `InvalidOperationException`, если состояние с таким `Id` уже зарегистрировано.

**`Register(IState<T> state)`** *(protected)*

- Удобная перегрузка: регистрирует состояние по его собственному `Id`.

**`Remove(T stateId)`**

- Проверяет `stateId` на `null`.
- Бросает `InvalidOperationException`, если состояние не зарегистрировано.
- Бросает `InvalidOperationException`, если пытаются удалить **активное** состояние.

**`SetState(T stateId)`**

- Проверяет `stateId` на `null`.
- Бросает `InvalidOperationException`, если состояние не зарегистрировано.
- Если запрошенное состояние уже активно — ничего не делает (ранний выход).
- Иначе: вызывает `Exit()` у текущего (если есть), затем `Enter()` у нового, обновляет `_currentStateId` и `_hasCurrentState`.

---

## Жизненный цикл состояний

```
        ┌─────────────┐
        │  No state   │
        └──────┬──────┘
               │ SetState(A)
               ▼
        ┌─────────────┐   SetState(B)   ┌─────────────┐
        │  State A    │ ──────────────► │  State B    │
        │  Enter()    │                 │  Enter()    │
        └─────────────┘ ◄────────────── └─────────────┘
                          SetState(A)
```

Порядок вызовов при переключении `A → B`:

1. `A.Exit()`
2. `B.Enter()`
3. `_currentStateId = B`

При первом вызове `SetState` (когда активного состояния ещё нет):

1. `Enter()` у выбранного состояния
2. `_currentStateId` устанавливается, `_hasCurrentState = true`

---

## Пример использования

```csharp
using System;
using Utils;

public enum GameState
{
    Menu,
    Playing,
    Paused
}

public sealed class MenuState : IState<GameState>
{
    public GameState Id => GameState.Menu;

    public void Enter() => Console.WriteLine("Enter Menu");
    public void Exit()  => Console.WriteLine("Exit Menu");
}

public sealed class PlayingState : IState<GameState>
{
    public GameState Id => GameState.Playing;

    public void Enter() => Console.WriteLine("Enter Playing");
    public void Exit()  => Console.WriteLine("Exit Playing");
}

public sealed class PausedState : IState<GameState>
{
    public GameState Id => GameState.Paused;

    public void Enter() => Console.WriteLine("Enter Paused");
    public void Exit()  => Console.WriteLine("Exit Paused");
}

public static class Program
{
    public static void Main()
    {
        var machine = new StateMachine<GameState>(new IState<GameState>[]
        {
            new MenuState(),
            new PlayingState(),
            new PausedState()
        });

        machine.SetState(GameState.Menu);    // Enter Menu
        machine.SetState(GameState.Playing); // Exit Menu → Enter Playing
        machine.SetState(GameState.Paused);  // Exit Playing → Enter Paused
        machine.SetState(GameState.Paused);  // (игнорируется)
        machine.SetState(GameState.Menu);    // Exit Paused → Enter Menu

        // machine.Remove(GameState.Menu);   // ❌ InvalidOperationException (активное состояние)
        machine.Remove(GameState.Playing);   // ✅ ок
    }
}
```

Вывод:

```
Enter Menu
Exit Menu
Enter Playing
Exit Playing
Enter Paused
Exit Paused
Enter Menu
```

---

## Обработка ошибок

Все исключения — `InvalidOperationException` или `ArgumentNullException`.

| Ситуация | Исключение |
|----------|------------|
| `Register` с уже существующим `Id` | `InvalidOperationException` |
| `Register` с `null` в `stateId` или `state` | `ArgumentNullException` |
| `Remove` незарегистрированного `Id` | `InvalidOperationException` |
| `Remove` активного состояния | `InvalidOperationException` |
| `SetState` незарегистрированного `Id` | `InvalidOperationException` |
| `SetState` с `null` в `stateId` | `ArgumentNullException` |

---

## Ограничения и замечания

- **Не потокобезопасно.** Для многопоточного использования требуется внешняя синхронизация.
- **Нет событий.** Переходы не сопровождаются событиями (`OnStateChanged` и т.п.) — при необходимости добавляются наследниками или обёрткой.
- **Нет истории/стека состояний.** Автомат хранит только текущее состояние; возврат «назад» реализуется вручную.
- **`CurrentState` — `protected`.** Доступен только наследникам; для внешнего чтения активного состояния нужно добавить публичное свойство.
- **Проверка `stateId == null`** для `enum`-типов избыточна в большинстве случаев (значимые типы не могут быть `null`), но безопасна и не мешает. При использовании `Nullable<T>` или ссылочных типов в качестве `T` (что ограничено `where T : Enum`) она не сработает — ограничение `Enum` гарантирует значимый тип.
- **Конструктор с `IEnumerable<IState<T>>`** использует `state.Id`; если два состояния имеют одинаковый `Id`, будет брошено исключение при регистрации второго.

---

## Расширение

Класс `StateMachine<T>` можно унаследовать для:

- добавления публичного `CurrentStateId`;
- логирования переходов;
- реализации стека состояний (`PushState`/`PopState`);
- добавления событий `StateChanged`.

Пример наследника с публичным доступом:

```csharp
public class ObservableStateMachine<T> : StateMachine<T> where T : Enum
{
    public event Action<T> StateChanged;

    public new void SetState(T stateId)
    {
        base.SetState(stateId);
        StateChanged?.Invoke(stateId);
    }
}
```

> ⚠️ Обратите внимание: `SetState` в базовом классе не `virtual`, поэтому используется `new`. Для корректного полиморфизма стоит сделать методы базового класса `virtual`.
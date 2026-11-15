#region USINGS

using System;
using UniStateMachine.Runtime;
using UnityEditorInternal;
using UnityEngine;
using D = UnityEngine.Debug;

#endregion USINGS

public enum GameStateId
{
    Inner,
    Gameplay,
    Pause
}

internal sealed class Test : MonoBehaviour
{
    public StringKeyStateMachine stateMachine;

    public GameStateId state;

    IState st1;
    IState st2;
    IState st3;

    public void Awake()
    {
        stateMachine.Start();
    }

    public void Start()
    {
        st1 = stateMachine.TryGetState(GameStateId.Inner.ToString());
        st1.OnEnter += () => D.Log($"I am {GameStateId.Inner}");
        st1.OnExit += () => D.Log($"I am exit {GameStateId.Inner}");

        st2 = stateMachine.TryGetState(GameStateId.Gameplay.ToString());
        st2.OnEnter += () => D.Log($"I am {GameStateId.Gameplay}");
        st2.OnExit += () => D.Log($"I am exit {GameStateId.Gameplay}");

        st3 = stateMachine.TryGetState(GameStateId.Pause.ToString());
        st3.OnEnter += () => D.Log($"I am {GameStateId.Pause}");
        st3.OnExit += () => D.Log($"I am exit {GameStateId.Pause}");
    }

    [ContextMenu("SetState")]
    public void SetState()
    {
        stateMachine.SetState(state.ToString());
    }
}
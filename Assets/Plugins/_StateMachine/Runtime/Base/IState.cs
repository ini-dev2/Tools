#region USINGS

using System;

#endregion USINGS

namespace UniStateMachine.Runtime
{
    public interface IState : IDisposable
    {
        event Action OnEnter;
        event Action OnExit;
    }

    public interface IState<T> : IState
    {
        public T Id { get; }
        public T NextId { get; }

        void Enter();
        void Exit();
    }
}
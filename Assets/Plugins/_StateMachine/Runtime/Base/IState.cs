#region USINGS

using System;

#endregion USINGS

namespace UniStateMachine.Runtime
{
    public interface IState <T>
    {
        event Action OnEnter;
        event Action OnExit;

        public T Id { get; }

        void Enter();
        void Exit();
    }
}
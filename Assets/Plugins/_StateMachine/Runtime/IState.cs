#region USINGS

using System;

#endregion USINGS

namespace UniStateMachine.Runtime
{
    public interface IState<T> where T : Enum
    {
        public T Id { get; }
        void Enter();
        void Exit();
    }
}
#region USINGS

using System;

#endregion USINGS

namespace UniStateMachine.Runtime
{
    public interface IStateMachine<T> where T : Enum
    {
        void SetState(T stateId);
        void Register(T stateId, IState<T> state);
        void Remove(T stateId);
    }
}
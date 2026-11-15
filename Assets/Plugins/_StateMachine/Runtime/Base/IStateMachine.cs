#region USINGS

using System;

#endregion USINGS

namespace UniStateMachine.Runtime
{
    public interface IStateMachine<T>
    {
        void SetState(T stateId);

        void Register(T stateId, IState<T> state);
        void Register(IState<T> state);

        void Remove(T stateId);
        void Remove(IState<T> state);
    }
}
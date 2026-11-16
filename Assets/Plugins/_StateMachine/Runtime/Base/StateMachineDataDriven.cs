#region USINGS

using System;
using System.Collections.Generic;
using UnityEngine;
using D = UnityEngine.Debug;

#endregion USINGS

namespace UniStateMachine.Runtime
{
    public class StateMachineDataDriven : ScriptableObject, IStateMachine<string>
    {
        private readonly StateMachine<string> stateMachine = new();

        public void Register(string stateId, IState<string> state)
        {
            stateMachine.Register(stateId, state);
        }

        public void Register(IState<string> state)
        {
            stateMachine.Register(state);
        }

        public void Remove(string stateId)
        {
            stateMachine.Remove(stateId);
        }

        public void Remove(IState<string> state)
        {
            stateMachine.Remove(state);
        }

        public void SetState(string stateId)
        {
            stateMachine.SetState(stateId);
        }

        public IState TryGetState(string stateId)
        {
            return stateMachine.TryGetState(stateId);
        }

        public void Dispose()
        {
            stateMachine.Dispose();
        }
    }
}
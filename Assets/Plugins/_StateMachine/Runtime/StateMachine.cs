#region USINGS

using System;
using System.Collections.Generic;

#endregion USINGS

namespace UniStateMachine.Runtime
{
    public class StateMachine<T> : IStateMachine<T> where T : Enum
    {
        private readonly Dictionary<T, IState<T>> _states = new();
        private T _currentStateId;

        protected IState<T> CurrentState => _states[_currentStateId];

        private bool _hasCurrentState;

        public StateMachine()
        {
        }

        public StateMachine(IEnumerable<IState<T>> states)
        {
            foreach (var state in states)
            {
                Register(state);
            }
        }

        public void Register(T stateId, IState<T> state)
        {
            if (stateId == null)
            {
                throw new ArgumentNullException(nameof(stateId));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (_states.ContainsKey(stateId))
            {
                throw new InvalidOperationException(
                    $"State with id '{stateId}' is already registered.");
            }

            _states.Add(stateId, state);
        }

        protected void Register(IState<T> state)
        {
            Register(state.Id, state);
        }

        public void Remove(T stateId)
        {
            if (stateId == null)
            {
                throw new ArgumentNullException(nameof(stateId));
            }

            if (!_states.ContainsKey(stateId))
            {
                throw new InvalidOperationException(
                    $"State with id '{stateId}' is not registered.");
            }

            if (_hasCurrentState && EqualityComparer<T>.Default.Equals(stateId, _currentStateId))
            {
                throw new InvalidOperationException(
                    $"Cannot remove currently active state '{stateId}'. " +
                    $"Switch to another state first.");
            }

            _states.Remove(stateId);
        }

        public void SetState(T stateId)
        {
            if (stateId == null)
            {
                throw new ArgumentNullException(nameof(stateId));
            }

            if (!_states.TryGetValue(stateId, out var nextState))
            {
                throw new InvalidOperationException(
                    $"State with id '{stateId}' is not registered.");
            }

            if (_hasCurrentState && EqualityComparer<T>.Default.Equals(stateId, _currentStateId))
            {
                return;
            }

            if (_hasCurrentState)
            {
                _states[_currentStateId].Exit();
            }

            nextState.Enter();

            _currentStateId = stateId;
            _hasCurrentState = true;
        }
    }
}
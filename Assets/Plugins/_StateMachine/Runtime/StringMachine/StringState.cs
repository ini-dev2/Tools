#region USINGS

using System;
using UnityEngine;

#endregion USINGS

namespace UniStateMachine.Runtime
{
    [Serializable]
    public sealed class StringState : IState<string>
    {
        [SerializeField] private string _id;

        [SerializeField] private string _nextNodeId;

        [HideInInspector]
        private StringKeyStateMachine _stringKeyStateMachine;

        public string Id => _id;

#if UNITY_EDITOR
        public StringState(string id)
        {
            _id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public StringState(string id, string nextNodeId, StringKeyStateMachine stringKeyStateMachine) : this(id)
        {
            _nextNodeId = nextNodeId ?? throw new ArgumentNullException(nameof(nextNodeId));
            _stringKeyStateMachine = stringKeyStateMachine ?? throw new ArgumentNullException(nameof(stringKeyStateMachine));
        }
#endif

        public event Action OnEnter;
        public event Action OnExit;

        public void Enter()
        {
            OnEnter?.Invoke();
        }

        public void Exit()
        {
            if (!string.IsNullOrEmpty(_nextNodeId))
            {
                _stringKeyStateMachine.SetState(_nextNodeId);
            }

            OnExit?.Invoke();
        }
    }
}
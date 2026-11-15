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

        public string Id => _id;
        public string NextId => _nextNodeId;


#if UNITY_EDITOR
        public StringState(string id)
        {
            _id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public StringState(string id, string nextNodeId) : this(id)
        {
            _nextNodeId = nextNodeId ?? throw new ArgumentNullException(nameof(nextNodeId));
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
            OnExit?.Invoke();
        }

        public void Dispose()
        {
            OnEnter = null;
            OnExit = null;
        }
    }
}
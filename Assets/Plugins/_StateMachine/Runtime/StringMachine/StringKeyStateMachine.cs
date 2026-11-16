#region USINGS

using System;
using System.Collections.Generic;
using UnityEngine;
using D = UnityEngine.Debug;

#endregion USINGS

namespace UniStateMachine.Runtime
{
    public sealed class StringKeyStateMachine : StateMachineDataDriven
    {
        [SerializeField] private StringState[] _states;

        public void Start()
        {
            foreach (var item in _states)
            {
                Register(item);
            }
        }

#if UNITY_EDITOR
        [NonSerialized] public List<StringState> editorStates = new();

        public void Build()
        {
            _states = editorStates.ToArray();
        }
#endif
    }
}
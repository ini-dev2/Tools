#region USINGS

using System;
using System.Collections.Generic;
using UnityEngine;
using D = UnityEngine.Debug;

#endregion USINGS

namespace UniStateMachine.Runtime
{
    public sealed class StringKeyStateMachine : StateMachineDataDriven<string>
    {
        [SerializeField] private StringState[] _states;

#if UNITY_EDITOR
        [NonSerialized] public List<StringState> editorStates = new();

        public void Build()
        {
            _states = editorStates.ToArray();
        }
#endif
    }
}
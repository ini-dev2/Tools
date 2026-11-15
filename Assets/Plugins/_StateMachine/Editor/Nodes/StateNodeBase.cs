#region USINGS

using System;

#endregion USINGS

namespace UniStateMachine.Editor
{
    [Serializable]
    public abstract class StateNodeBase : BaseNode
    {
        public abstract bool TryGetStateId(out Enum stateId);
    }
}
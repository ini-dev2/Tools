#region USINGS

using System;

#endregion USINGS

namespace UniStateMachine.Editor
{

    [Serializable]
    public abstract class StateNode<TStateId> : StateNodeBase where TStateId : Enum
    {
        public static readonly string StateFieldName = "CurrentState";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            AddInputOutputExecutionPorts(context, outputName: "Next State");
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<TStateId>(StateFieldName)
                .WithDisplayName(StateFieldName)
                .Build();
        }

        public override bool TryGetStateId(out Enum stateId)
        {
            var option = GetNodeOptionByName(StateFieldName);
            if (option.TryGetValue<TStateId>(out var id))
            {
                stateId = id;
                return true;
            }
            stateId = default;
            return false;
        }
    }
}
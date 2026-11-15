#region USINGS

using System;
using Unity.GraphToolkit.Editor;
using static Unity.GraphToolkit.Editor.Node;
using D = UnityEngine.Debug;

#endregion USINGS

namespace UniStateMachine.Editor
{
    [Serializable]
    public abstract class BaseNode : Node
    {
        public const string EXECUTION_PORT_DEFAULT_NAME = "ExecutionPort";

        protected void AddInputOutputExecutionPorts(IPortDefinitionContext context, string inputName = "", string outputName = "")
        {
            context.AddInputPort(EXECUTION_PORT_DEFAULT_NAME)
                .WithDisplayName(inputName)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            context.AddOutputPort(EXECUTION_PORT_DEFAULT_NAME)
                .WithDisplayName(outputName)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }
    }
}
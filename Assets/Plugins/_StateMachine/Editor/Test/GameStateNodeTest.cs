#region USINGS

using System;
using Unity.GraphToolkit.Editor;

#endregion USINGS

namespace UniStateMachine.Editor
{
    [Serializable]
    [UseWithGraph(typeof(StateMachineGraph))]
    internal class GameStateNodeTest : StateNode<GameStateId>
    {
    }
}
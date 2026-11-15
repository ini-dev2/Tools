#region USINGS

using System;
using System.Collections.Generic;
using System.Linq;
using UniStateMachine.Runtime;
using Unity.GraphToolkit.Editor;
using UnityEditor.AssetImporters;
using UnityEngine;
using D = UnityEngine.Debug;

#endregion USINGS

namespace UniStateMachine.Editor
{
    [ScriptedImporter(1, StateMachineGraph.AssetExtension)]
    internal sealed class GraphRuntimeImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var graph = GraphDatabase.LoadGraphForImporter<StateMachineGraph>(ctx.assetPath);
            if (graph == null)
            {
                D.LogError($"Failed to load graph asset: {ctx.assetPath}");
                return;
            }

            var startNodeModel = graph.GetNodes().OfType<StartNode>().FirstOrDefault();
            if (startNodeModel == null) return;

            var runtimeAsset = ScriptableObject.CreateInstance<StringKeyStateMachine>();

            // Первый этап: BFS собирает все узлы состояний и записывает отношение "текущий узел → следующие узлы"
            var visited = new HashSet<INode>();
            var queue = new Queue<INode>();
            var nextMap = new Dictionary<INode, List<INode>>();

            foreach (var next in GetNextNodes(startNodeModel))
                queue.Enqueue(next);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == null || !visited.Add(current)) continue;

                var nextNodes = GetNextNodes(current);
                nextMap[current] = nextNodes;

                foreach (var next in nextNodes)
                    queue.Enqueue(next);
            }

            foreach (var kvp in nextMap)
            {
                var node = kvp.Key;
                if (node is not StateNodeBase stateNode) continue;
                if (!stateNode.TryGetStateId(out var stateId)) continue;

                string nextId = null;
                var nextNodes = kvp.Value;
                if (nextNodes.Count > 0 && nextNodes[0] is StateNodeBase nextStateNode)
                {
                    if (nextStateNode.TryGetStateId(out var nextEnum))
                        nextId = nextEnum.ToString();
                }

                runtimeAsset.editorStates.Add(
                    new StringState(stateId.ToString(), nextId ?? string.Empty, runtimeAsset)
                );
            }

            runtimeAsset.Build();

            ctx.AddObjectToAsset("RuntimeAsset", runtimeAsset);
            ctx.SetMainObject(runtimeAsset);
        }

        static List<INode> GetNextNodes(INode currentNode)
        {
            var result = new List<INode>();
            var outputPort = currentNode.GetOutputPortByName(BaseNode.EXECUTION_PORT_DEFAULT_NAME);
            if (outputPort == null) return result;

            var connectedPorts = new List<IPort>();
            outputPort.GetConnectedPorts(connectedPorts);

            foreach (var port in connectedPorts)
            {
                var node = port.GetNode();
                if (node != null) result.Add(node);
            }
            return result;
        }
    }
}
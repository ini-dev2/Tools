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
                D.LogError($"[{nameof(GraphRuntimeImporter)}] Failed to load graph asset: {ctx.assetPath}");
                return;
            }

            var startNodeModel = graph.GetNodes().OfType<StartNode>().FirstOrDefault();
            if (startNodeModel == null)
            {
                D.LogError($"[{nameof(GraphRuntimeImporter)}] No StartNode found in graph: {ctx.assetPath}");
                return;
            }

            var runtimeAsset = ScriptableObject.CreateInstance<StringKeyStateMachine>();

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

            if (nextMap.Count == 0)
            {
                D.LogError($"[{nameof(GraphRuntimeImporter)}] Graph has no reachable state nodes from StartNode: {ctx.assetPath}");
                return;
            }

            var usedIds = new HashSet<string>();

            foreach (var kvp in nextMap)
            {
                var node = kvp.Key;
                if (node is not StateNodeBase stateNode) continue;
                if (!stateNode.TryGetStateId(out var stateId))
                {
                    D.LogWarning($"[{nameof(GraphRuntimeImporter)}] StateNode {node.GetType().Name} has no enum value. Skipping.");
                    continue;
                }

                string id = stateId.ToString();

                if (!usedIds.Add(id))
                {
                    D.LogError($"[{nameof(GraphRuntimeImporter)}] Duplicate state id '{id}' detected in graph '{ctx.assetPath}'. Node: {node.GetType().Name}. Skipping duplicate.");
                    continue;
                }

                var nextNodes = kvp.Value;

                if (nextNodes.Count > 1)
                {
                    D.LogWarning($"[{nameof(GraphRuntimeImporter)}] State '{id}' has {nextNodes.Count} outgoing branches. Only the first one will be used. Multiple branches are not supported by StringState.");
                }

                string nextId = null;
                if (nextNodes.Count > 0 && nextNodes[0] is StateNodeBase nextStateNode)
                {
                    if (nextStateNode.TryGetStateId(out var nextEnum))
                        nextId = nextEnum.ToString();
                    else
                        D.LogWarning($"[{nameof(GraphRuntimeImporter)}] Next node of state '{id}' has no enum value. nextId will be empty.");
                }
                else if (nextNodes.Count > 0)
                {
                    D.LogWarning($"[{nameof(GraphRuntimeImporter)}] Next node of state '{id}' is not a StateNodeBase ({nextNodes[0].GetType().Name}). nextId will be empty.");
                }

                if (!string.IsNullOrEmpty(nextId) && !usedIds.Contains(nextId))
                {
                }

                runtimeAsset.editorStates.Add(
                    new StringState(id, nextId ?? string.Empty)
                );
            }

            var allIds = new HashSet<string>(runtimeAsset.editorStates.Select(s => s.Id));
            foreach (var state in runtimeAsset.editorStates)
            {
                var nextId = GetNextId(state);
                if (!string.IsNullOrEmpty(nextId) && !allIds.Contains(nextId))
                {
                    D.LogError($"[{nameof(GraphRuntimeImporter)}] State '{state.Id}' references non-existent next state '{nextId}' in graph '{ctx.assetPath}'.");
                }
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
        static string GetNextId(StringState state) => null;
    }
}
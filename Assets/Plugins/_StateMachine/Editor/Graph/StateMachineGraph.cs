#region USINGS

using System;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using D = UnityEngine.Debug;

#endregion USINGS

namespace UniStateMachine.Editor
{
    [Graph(AssetExtension)]
    [Serializable]
    internal sealed class StateMachineGraph : Graph
    {
        public const string AssetExtension = "StateGraph";

        [MenuItem("Tools/KolaTech/StateMachineGraph", priority = 100)]
        static void CreateAssetFile()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<StateMachineGraph>();
        }
    }
}
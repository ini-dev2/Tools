using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScriptsSamples.Editor
{
    internal static class ScriptTemplateCreator
    {
        private const string MenuPath = "Assets/Create/kolatech/";
        private const string RootFolderName = "_ScriptsSamples";
        private const string ConfigsFolderName = "Configs";

        private static string s_templatesFolder;

        [MenuItem(MenuPath + "C# Templates/AssemblyInfo", false, 1)]
        private static void CreateAssemblyInfo() =>
            Create("AssemblyInfo.cs.txt", "AssemblyInfo.cs");

        [MenuItem(MenuPath + "C# Templates/Interface", false, 2)]
        private static void CreateInterface() =>
            Create("Interface.cs.txt", "IInterface.cs");

        [MenuItem(MenuPath + "C# Templates/MonoBehClass", false, 3)]
        private static void CreateMonobehaviour() =>
            Create("MonoBehClass.cs.txt", "MonoBehClass.cs");

        [MenuItem(MenuPath + "C# Templates/C# Class", false, 4)]
        private static void CreateSharp() =>
            Create("CSharpClass.cs.txt", "CSharpClass.cs");

        [MenuItem(MenuPath + "C# Templates/MonoInstaller", false, 5)]
        private static void CreateMonoInstaller() =>
            Create("MonoInstaller.cs.txt", "MonoInstaller.cs");

        private static void Create(string templateName, string defaultFileName)
        {
            var templatesFolder = GetTemplatesFolder();

            if (templatesFolder == null)
            {
                Debug.LogError($"Не удалось найти папку '{RootFolderName}/{ConfigsFolderName}'.");
                return;
            }

            var templatePath = Path.Combine(templatesFolder, templateName).Replace('\\', '/');

            if (!File.Exists(templatePath))
            {
                Debug.LogError($"Template not found:\n{templatePath}");
                return;
            }

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(
                templatePath,
                defaultFileName);
        }

        private static string GetTemplatesFolder()
        {
            if (!string.IsNullOrEmpty(s_templatesFolder))
                return s_templatesFolder;

            var guids = AssetDatabase.FindAssets(RootFolderName);

            foreach (var guid in guids)
            {
                var rootPath = AssetDatabase.GUIDToAssetPath(guid);

                if (!AssetDatabase.IsValidFolder(rootPath))
                    continue;

                var configsPath = Path.Combine(rootPath, ConfigsFolderName).Replace('\\', '/');

                if (!AssetDatabase.IsValidFolder(configsPath))
                    continue;

                s_templatesFolder = configsPath;
                return s_templatesFolder;
            }

            return null;
        }
    }
}
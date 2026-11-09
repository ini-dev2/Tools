#region USINGS

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

#endregion USINGS

namespace ScriptsSamples.Editor
{
    internal sealed class FolderGenerateItem
    {
        public string FolderName { get; set; }
    }

    internal sealed class FolderGenerateSettings
    {
        private static readonly string[] DefaultFolderNames =
        {
            "Application",
            "Game",
            "Presentations",
            "Utils",
            "Infrastructure"
        };

        public string ProjectName { get; set; } = string.Empty;

        public string BasePath { get; set; } = "Assets";

        public List<FolderGenerateItem> Items { get; } =
            new List<FolderGenerateItem>();

        public void InitializeDefaults()
        {
            if (Items.Count > 0)
            {
                return;
            }

            foreach (var folderName in DefaultFolderNames)
            {
                Items.Add(
                    new FolderGenerateItem
                    {
                        FolderName = folderName
                    });
            }
        }
    }

    internal static class FolderGenerateSettingsStorage
    {
        private const string ProjectNameKey =
            "KolaTech.FolderGenerate.ProjectName";

        private const string BasePathKey =
            "KolaTech.FolderGenerate.BasePath";

        private const string ItemsKey =
            "KolaTech.FolderGenerate.Items";

        public static FolderGenerateSettings Load()
        {
            var settings = new FolderGenerateSettings
            {
                ProjectName = EditorPrefs.GetString(
                    ProjectNameKey,
                    string.Empty),

                BasePath = EditorPrefs.GetString(
                    BasePathKey,
                    "Assets")
            };

            var serializedItems = EditorPrefs.GetString(
                ItemsKey,
                string.Empty);

            if (!string.IsNullOrEmpty(serializedItems))
            {
                var items = DeserializeItems(
                    serializedItems);

                foreach (var item in items)
                {
                    settings.Items.Add(item);
                }
            }

            settings.InitializeDefaults();

            return settings;
        }

        public static void Save(
            FolderGenerateSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            EditorPrefs.SetString(
                ProjectNameKey,
                settings.ProjectName ?? string.Empty);

            EditorPrefs.SetString(
                BasePathKey,
                settings.BasePath ?? string.Empty);

            EditorPrefs.SetString(
                ItemsKey,
                SerializeItems(settings.Items));
        }

        private static string SerializeItems(
            List<FolderGenerateItem> items)
        {
            var builder = new StringBuilder();

            foreach (var item in items)
            {
                var folderName = Uri.EscapeDataString(
                    item.FolderName ?? string.Empty);

                builder.Append(folderName);
                builder.Append('\n');
            }

            return builder.ToString();
        }

        private static List<FolderGenerateItem> DeserializeItems(
            string serializedItems)
        {
            var result =
                new List<FolderGenerateItem>();

            var lines = serializedItems.Split(
                new[] { '\n' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                result.Add(
                    new FolderGenerateItem
                    {
                        FolderName =
                            Uri.UnescapeDataString(line)
                    });
            }

            return result;
        }
    }

    internal static class FolderGenerateService
    {
        public static void Generate(
            FolderGenerateSettings settings)
        {
            if (!Validate(
                    settings,
                    out var error))
            {
                EditorUtility.DisplayDialog(
                    "Create Folder",
                    error,
                    "OK");

                return;
            }

            foreach (var item in settings.Items)
            {
                var folderPath =
                    NormalizePath(
                        Path.Combine(
                            settings.BasePath,
                            item.FolderName));

                if (!AssetDatabase.IsValidFolder(
                        folderPath))
                {
                    CreateFolder(folderPath);
                }

                var assemblyName =
                    BuildAssemblyName(
                        settings.ProjectName,
                        item.FolderName);

                var assemblyDefinitionPath =
                    NormalizePath(
                        Path.Combine(
                            folderPath,
                            assemblyName + ".asmdef"));

                if (File.Exists(
                        assemblyDefinitionPath))
                {
                    Debug.LogWarning(
                        "Assembly definition already exists: " +
                        assemblyDefinitionPath);

                    continue;
                }

                CreateAssemblyDefinition(
                    assemblyDefinitionPath,
                    assemblyName);
            }

            AssetDatabase.Refresh();

            Debug.Log(
                "Created " +
                settings.Items.Count +
                " folder(s).");
        }

        private static bool Validate(
            FolderGenerateSettings settings,
            out string error)
        {
            if (settings == null)
            {
                error = "Settings are null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.ProjectName))
            {
                error = "Project Name cannot be empty.";
                return false;
            }

            if (!IsValidIdentifier(
                    settings.ProjectName))
            {
                error =
                    "Project Name must contain only letters, " +
                    "digits and underscores and cannot start with a digit.";

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.BasePath))
            {
                error = "Base Path cannot be empty.";
                return false;
            }

            settings.BasePath =
                NormalizePath(settings.BasePath);

            if (!settings.BasePath.Equals(
                    "Assets",
                    StringComparison.Ordinal) &&
                !settings.BasePath.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal))
            {
                error =
                    "Base Path must be inside the Unity Assets folder.";

                return false;
            }

            if (settings.Items == null ||
                settings.Items.Count == 0)
            {
                error = "Add at least one item.";
                return false;
            }

            foreach (var item in settings.Items)
            {
                if (item == null)
                {
                    error =
                        "List contains a null item.";

                    return false;
                }

                if (string.IsNullOrWhiteSpace(
                        item.FolderName))
                {
                    error =
                        "Folder Name cannot be empty.";

                    return false;
                }

                if (!IsValidIdentifier(
                        item.FolderName))
                {
                    error =
                        "Folder Name '" +
                        item.FolderName +
                        "' is invalid.";

                    return false;
                }
            }

            var duplicateFolders =
                settings.Items
                    .GroupBy(
                        x => x.FolderName,
                        StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(
                        x => x.Count() > 1);

            if (duplicateFolders != null)
            {
                error =
                    "Duplicate folder name: " +
                    duplicateFolders.Key;

                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsValidIdentifier(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (char.IsDigit(value[0]))
            {
                return false;
            }

            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];

                if (!char.IsLetterOrDigit(character) &&
                    character != '_')
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildAssemblyName(
            string projectName,
            string folderName)
        {
            return projectName + "." + folderName;
        }

        private static void CreateFolder(
            string folderPath)
        {
            var parts =
                folderPath.Split('/');

            var currentPath = parts[0];

            for (var i = 1;
                 i < parts.Length;
                 i++)
            {
                var nextPath =
                    currentPath + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(
                        nextPath))
                {
                    AssetDatabase.CreateFolder(
                        currentPath,
                        parts[i]);
                }

                currentPath = nextPath;
            }
        }

        private static void CreateAssemblyDefinition(
            string path,
            string assemblyName)
        {
            var json =
                "{\n" +
                "    \"name\": \"" +
                EscapeJson(assemblyName) +
                "\",\n" +
                "    \"rootNamespace\": \"" +
                EscapeJson(assemblyName) +
                "\"\n" +
                "}";

            File.WriteAllText(
                path,
                json,
                Encoding.UTF8);
        }

        private static string EscapeJson(
            string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private static string NormalizePath(
            string path)
        {
            return path
                .Replace('\\', '/')
                .TrimEnd('/');
        }
    }

    internal sealed class FolderGenerate : EditorWindow
    {
        private const string WindowTitle =
            "KolaTech Create Folder";

        private FolderGenerateSettings _settings;

        private Vector2 _scrollPosition;

        [MenuItem("Tools/KolaTech/CreateFolder")]
        private static void Open()
        {
            var window =
                GetWindow<FolderGenerate>();

            window.titleContent =
                new GUIContent(WindowTitle);

            window.minSize =
                new Vector2(600f, 400f);

            window.Show();
        }

        private void OnEnable()
        {
            _settings =
                FolderGenerateSettingsStorage.Load();
        }

        private void OnDisable()
        {
            Save();
        }

        private void OnGUI()
        {
            if (_settings == null)
            {
                _settings =
                    FolderGenerateSettingsStorage.Load();
            }

            DrawProjectName();

            EditorGUILayout.Space(10);

            DrawBasePath();

            EditorGUILayout.Space(10);

            DrawItems();

            EditorGUILayout.Space(10);

            DrawActions();
        }

        private void DrawProjectName()
        {
            EditorGUILayout.LabelField(
                "Project Name",
                EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            _settings.ProjectName =
                EditorGUILayout.TextField(
                    _settings.ProjectName);

            if (EditorGUI.EndChangeCheck())
            {
                Save();
            }

            EditorGUILayout.HelpBox(
                "Assembly Definition names will be generated as " +
                "'ProjectName.FolderName'.",
                MessageType.Info);
        }

        private void DrawBasePath()
        {
            EditorGUILayout.LabelField(
                "Base Path",
                EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();

            _settings.BasePath =
                EditorGUILayout.TextField(
                    _settings.BasePath);

            if (EditorGUI.EndChangeCheck())
            {
                Save();
            }

            if (GUILayout.Button(
                    "Select",
                    GUILayout.Width(70f)))
            {
                SelectBasePath();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawItems()
        {
            EditorGUILayout.LabelField(
                "Folders",
                EditorStyles.boldLabel);

            _scrollPosition =
                EditorGUILayout.BeginScrollView(
                    _scrollPosition);

            for (var i = 0;
                 i < _settings.Items.Count;
                 i++)
            {
                DrawItem(i);

                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Add"))
            {
                _settings.Items.Add(
                    new FolderGenerateItem
                    {
                        FolderName =
                            string.Empty
                    });

                Save();
            }
        }

        private void DrawItem(
            int index)
        {
            var item =
                _settings.Items[index];

            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(
                "Element " + (index + 1),
                EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(
                    "Remove",
                    GUILayout.Width(70f)))
            {
                _settings.Items.RemoveAt(index);

                Save();

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                GUIUtility.ExitGUI();

                return;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();

            item.FolderName =
                EditorGUILayout.TextField(
                    "Folder Name",
                    item.FolderName);

            var assemblyName =
                BuildAssemblyNamePreview(
                    item.FolderName);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(
                    "Assembly Definition",
                    assemblyName);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Save();
            }

            EditorGUILayout.EndVertical();
        }

        private string BuildAssemblyNamePreview(
            string folderName)
        {
            if (string.IsNullOrWhiteSpace(
                    _settings.ProjectName) ||
                string.IsNullOrWhiteSpace(
                    folderName))
            {
                return string.Empty;
            }

            return _settings.ProjectName +
                   "." +
                   folderName;
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Save"))
            {
                Save();
            }

            GUI.enabled =
                _settings.Items.Count > 0;

            if (GUILayout.Button(
                    "Create",
                    GUILayout.Height(30f)))
            {
                Save();

                FolderGenerateService.Generate(
                    _settings);
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        private void SelectBasePath()
        {
            var absolutePath =
                EditorUtility.OpenFolderPanel(
                    "Select Base Folder",
                    Application.dataPath,
                    string.Empty);

            if (string.IsNullOrEmpty(
                    absolutePath))
            {
                return;
            }

            var normalizedAbsolutePath =
                NormalizeAbsolutePath(
                    absolutePath);

            var normalizedAssetsPath =
                NormalizeAbsolutePath(
                    Application.dataPath);

            var isInsideAssets =
                normalizedAbsolutePath.Equals(
                    normalizedAssetsPath,
                    StringComparison.OrdinalIgnoreCase) ||
                normalizedAbsolutePath.StartsWith(
                    normalizedAssetsPath + "/",
                    StringComparison.OrdinalIgnoreCase);

            if (!isInsideAssets)
            {
                EditorUtility.DisplayDialog(
                    "Invalid Path",
                    "Folder must be located inside " +
                    "the Unity Assets folder.",
                    "OK");

                return;
            }

            var relativePath =
                normalizedAbsolutePath.Substring(
                    normalizedAssetsPath.Length);

            relativePath =
                relativePath.TrimStart('/');

            _settings.BasePath =
                string.IsNullOrEmpty(relativePath)
                    ? "Assets"
                    : "Assets/" + relativePath;

            Save();

            Repaint();
        }

        private void Save()
        {
            FolderGenerateSettingsStorage.Save(
                _settings);
        }

        private static string NormalizeAbsolutePath(
            string path)
        {
            return path
                .Replace('\\', '/')
                .TrimEnd('/');
        }
    }
}
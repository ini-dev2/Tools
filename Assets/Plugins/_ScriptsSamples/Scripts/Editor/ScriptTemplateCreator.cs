using System;
using System.IO;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace ScriptsSamples.Editor
{
    internal sealed class ScriptTemplateDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string TemplateFile { get; }
        public string DefaultFileName { get; }

        public ScriptTemplateDefinition(
            string id, string displayName,
            string templateFile, string defaultFileName)
        {
            Id = id;
            DisplayName = displayName;
            TemplateFile = templateFile;
            DefaultFileName = defaultFileName;
        }
    }

    internal static class ScriptTemplateCreator
    {
        private const string MenuPath = "Assets/Create/kolatech/";
        private const string RootFolderName = "_ScriptsSamples";
        private const string ConfigsFolderName = "Configs";
        internal const string ShortcutPrefix = "KolaTech/Templates/";

        internal static readonly ScriptTemplateDefinition[] Templates =
        {
            new ScriptTemplateDefinition(
                "AssemblyInfo", "AssemblyInfo",
                "AssemblyInfo.cs.txt", "AssemblyInfo.cs"),
            new ScriptTemplateDefinition(
                "Interface", "Interface",
                "Interface.cs.txt", "IInterface.cs"),
            new ScriptTemplateDefinition(
                "MonoBehClass", "MonoBehClass",
                "MonoBehClass.cs.txt", "MonoBehClass.cs"),
            new ScriptTemplateDefinition(
                "CSharpClass", "C# Class",
                "CSharpClass.cs.txt", "CSharpClass.cs"),
            new ScriptTemplateDefinition(
                "MonoInstaller", "MonoInstaller",
                "MonoInstaller.cs.txt", "MonoInstaller.cs"),
        };

        private static string s_templatesFolder;

        [MenuItem(MenuPath + "C# Templates/AssemblyInfo", false, 1)]
        private static void CreateAssemblyInfo() => InvokeTemplate("AssemblyInfo");

        [MenuItem(MenuPath + "C# Templates/Interface", false, 2)]
        private static void CreateInterface() => InvokeTemplate("Interface");

        [MenuItem(MenuPath + "C# Templates/MonoBehClass", false, 3)]
        private static void CreateMonobehaviour() => InvokeTemplate("MonoBehClass");

        [MenuItem(MenuPath + "C# Templates/C# Class", false, 4)]
        private static void CreateSharp() => InvokeTemplate("CSharpClass");

        [MenuItem(MenuPath + "C# Templates/MonoInstaller", false, 5)]
        private static void CreateMonoInstaller() => InvokeTemplate("MonoInstaller");

        internal static void ExecuteById(string templateId) => InvokeTemplate(templateId);

        private static void InvokeTemplate(string templateId)
        {
            var def = FindDefinition(templateId);
            if (def == null)
            {
                Debug.LogError($"Unknown template id: {templateId}");
                return;
            }
            Create(def.TemplateFile, def.DefaultFileName);
        }

        private static ScriptTemplateDefinition FindDefinition(string templateId)
        {
            foreach (var t in Templates)
                if (t.Id == templateId)
                    return t;
            return null;
        }

        private static void Create(string templateName, string defaultFileName)
        {
            var templatesFolder = GetTemplatesFolder();
            if (templatesFolder == null)
            {
                Debug.LogError($"Не удалось найти папку '{RootFolderName}/{ConfigsFolderName}'.");
                return;
            }

            var templatePath = Path.Combine(templatesFolder, templateName)
                .Replace('\\', '/');

            if (!File.Exists(templatePath))
            {
                Debug.LogError($"Template not found:\n{templatePath}");
                return;
            }

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, defaultFileName);
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

                var configsPath = Path.Combine(rootPath, ConfigsFolderName)
                    .Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(configsPath))
                    continue;

                s_templatesFolder = configsPath;
                return s_templatesFolder;
            }
            return null;
        }
    }

    internal static class ScriptTemplateShortcuts
    {
        [Shortcut(ScriptTemplateCreator.ShortcutPrefix + "AssemblyInfo")]
        private static void AssemblyInfoShortcut() =>
            ScriptTemplateCreator.ExecuteById("AssemblyInfo");

        [Shortcut(ScriptTemplateCreator.ShortcutPrefix + "Interface")]
        private static void InterfaceShortcut() =>
            ScriptTemplateCreator.ExecuteById("Interface");

        [Shortcut(ScriptTemplateCreator.ShortcutPrefix + "MonoBehClass")]
        private static void MonoBehClassShortcut() =>
            ScriptTemplateCreator.ExecuteById("MonoBehClass");

        [Shortcut(ScriptTemplateCreator.ShortcutPrefix + "CSharpClass")]
        private static void CSharpClassShortcut() =>
            ScriptTemplateCreator.ExecuteById("CSharpClass");

        [Shortcut(ScriptTemplateCreator.ShortcutPrefix + "MonoInstaller")]
        private static void MonoInstallerShortcut() =>
            ScriptTemplateCreator.ExecuteById("MonoInstaller");

        [MenuItem("Tools/KolaTech/Templates Hotkeys")]
        private static void OpenWindow()
        {
            var w = EditorWindow.GetWindow<ScriptTemplateHotkeyWindow>(
                true, "Templates Hotkeys", true);
            w.minSize = new Vector2(560f, 360f);
            w.Show();
        }
    }

    internal sealed class ScriptTemplateHotkeyWindow : EditorWindow
    {
        private const double CaptureTimeout = 10.0;

        private string _capturingTemplateId;
        private double _captureStartTime;
        private Vector2 _scroll;

        // Состояние модификаторов, нажатых в данный момент
        private bool _ctrlHeld;
        private bool _shiftHeld;
        private bool _altHeld;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Назначение хоткеев", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Нажми Assign и введи комбинацию (сначала зажми модификаторы, затем нажми основную клавишу).\n" +
                "Esc — отмена.",
                MessageType.Info);

            EditorGUILayout.Space(6);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var t in ScriptTemplateCreator.Templates)
            {
                DrawRow(t);
                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_capturingTemplateId))
                HandleCaptureInput();
        }

        private void DrawRow(ScriptTemplateDefinition t)
        {
            var shortcutId = ScriptTemplateCreator.ShortcutPrefix + t.Id;

            string display = "None";
            try
            {
                var binding = ShortcutManager.instance.GetShortcutBinding(shortcutId);
                var seq = binding.keyCombinationSequence;

                if (seq != null)
                {
                    foreach (var combo in seq)
                    {
                        display = combo.ToString();
                        break;
                    }
                }
            }
            catch
            {
                // шорткат ещё не зарегистрирован
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(t.DisplayName, EditorStyles.boldLabel, GUILayout.Width(140f));

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField(display);

            bool isCapturing = _capturingTemplateId == t.Id;
            if (GUILayout.Button(isCapturing ? "Press..." : "Assign", GUILayout.Width(90f)))
            {
                _capturingTemplateId = isCapturing ? null : t.Id;
                _captureStartTime = EditorApplication.timeSinceStartup;
                ResetCaptureModifiers();
                Repaint();
            }

            if (GUILayout.Button("Clear", GUILayout.Width(60f)))
            {
                try
                {
                    EnsureWritableProfile();
                    ShortcutManager.instance.ClearShortcutOverride(shortcutId);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e.Message);
                }
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void ResetCaptureModifiers()
        {
            _ctrlHeld = false;
            _shiftHeld = false;
            _altHeld = false;
        }

        private void EnsureWritableProfile()
        {
            var manager = ShortcutManager.instance;

            if (manager.IsProfileReadOnly(manager.activeProfileId))
            {
                const string customProfileId = "KolaTech Custom";
                var available = manager.GetAvailableProfileIds();

                bool exists = false;
                foreach (var pid in available)
                {
                    if (pid == customProfileId) { exists = true; break; }
                }

                if (!exists)
                    manager.CreateProfile(customProfileId);

                manager.activeProfileId = customProfileId;
            }
        }

        private void HandleCaptureInput()
        {
            if (EditorApplication.timeSinceStartup - _captureStartTime > CaptureTimeout)
            {
                _capturingTemplateId = null;
                ResetCaptureModifiers();
                Repaint();
                return;
            }

            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown)
                return;

            // Esc — отмена
            if (e.keyCode == KeyCode.Escape)
            {
                _capturingTemplateId = null;
                ResetCaptureModifiers();
                e.Use();
                Repaint();
                return;
            }

            // Если нажат модификатор — только записываем состояние, не завершаем захват
            if (IsModifierKey(e.keyCode))
            {
                UpdateHeldModifiers(e);
                e.Use();
                Repaint();
                return;
            }

            // Если это настоящая клавиша, но не модификатор — завершаем захват
            if (e.keyCode != KeyCode.None)
            {
                UpdateHeldModifiers(e);

                var id = _capturingTemplateId;
                if (!string.IsNullOrEmpty(id))
                {
                    var shortcutId = ScriptTemplateCreator.ShortcutPrefix + id;

                    var modifiers = ShortcutModifiers.None;
                    if (_ctrlHeld) modifiers |= ShortcutModifiers.Action;
                    if (_shiftHeld) modifiers |= ShortcutModifiers.Shift;
                    if (_altHeld) modifiers |= ShortcutModifiers.Alt;

                    var keyCombo = new KeyCombination(e.keyCode, modifiers);
                    var binding = new ShortcutBinding(keyCombo);

                    try
                    {
                        EnsureWritableProfile();
                        ShortcutManager.instance.RebindShortcut(shortcutId, binding);
                        Debug.Log($"Назначен хоткей {keyCombo} для {id}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Не удалось назначить: {ex.Message}");
                    }
                }

                _capturingTemplateId = null;
                ResetCaptureModifiers();
                e.Use();
                Repaint();
            }
        }

        private static bool IsModifierKey(KeyCode key)
        {
            return key == KeyCode.LeftControl || key == KeyCode.RightControl ||
                   key == KeyCode.LeftShift || key == KeyCode.RightShift ||
                   key == KeyCode.LeftAlt || key == KeyCode.RightAlt;
        }

        private void UpdateHeldModifiers(Event e)
        {
            _ctrlHeld = e.control || e.command;
            _shiftHeld = e.shift;
            _altHeld = e.alt;
        }
    }
}
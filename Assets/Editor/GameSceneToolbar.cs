using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>Editor-only scene shortcuts. No player/build code or scene changes.</summary>
[InitializeOnLoad]
public static class GameSceneToolbar
{
    const string ElementName = "tcg-scene-shortcuts";
    static readonly Type ToolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
    static readonly FieldInfo ToolbarRoot = ToolbarType?.GetField("m_Root", BindingFlags.Instance | BindingFlags.NonPublic);
    static IMGUIContainer _button;
    static double _nextAttach;

    static GameSceneToolbar()
    {
        EditorApplication.update += AttachIfNeeded;
        EditorApplication.playModeStateChanged += _ => Refresh();
        EditorSceneManager.activeSceneChangedInEditMode += (_, __) => Refresh();
    }

    static void AttachIfNeeded()
    {
        if (_button != null && _button.panel != null) return;
        if (EditorApplication.timeSinceStartup < _nextAttach) return;
        _nextAttach = EditorApplication.timeSinceStartup + 1;
        // Unity 6.0 has no public API for adding controls to the main toolbar.
        // Limit reflection to finding its host; all UI and scene operations use public APIs.
        if (ToolbarType == null || ToolbarRoot == null) return;
        foreach (var toolbar in Resources.FindObjectsOfTypeAll(ToolbarType))
        {
            var root = ToolbarRoot.GetValue(toolbar) as VisualElement;
            var host = root?.Q("ToolbarZoneLeftAlign");
            if (host == null) continue;
            host.Q(ElementName)?.RemoveFromHierarchy();
            _button = new IMGUIContainer(() =>
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("Sahneler: " + Label(SceneManager.GetActiveScene().path), EditorStyles.toolbarDropDown, GUILayout.Width(168), GUILayout.Height(22)))
                        ShowScenes();
            }) { name = ElementName };
            _button.style.width = 168;
            _button.style.height = 22;
            _button.style.marginLeft = 8;
            _button.style.marginRight = 4;
            _button.style.alignSelf = Align.Center;
            _button.style.flexShrink = 0;
            host.Add(_button);
            Refresh();
            break;
        }
    }

    static void Refresh()
    {
        if (_button == null) return;
        _button.MarkDirtyRepaint();
        bool ready = !EditorApplication.isPlayingOrWillChangePlaymode;
        _button.SetEnabled(ready);
        _button.tooltip = ready ? "Oyunun sahneleri arasında geçiş yap" : "Sahne değiştirmek için Play modunu durdur.";
    }

    static string Label(string path)
    {
        switch (Path.GetFileNameWithoutExtension(path))
        {
            case "MenuScene": return "Ana Menü";
            case "MainScene": return "Oyun";
            case "BootScene": return "Başlangıç";
            default: return string.IsNullOrEmpty(path) ? "Seç" : Path.GetFileNameWithoutExtension(path);
        }
    }

    static void ShowScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var menu = new GenericMenu();
        var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        var paths = Array.ConvertAll(guids, AssetDatabase.GUIDToAssetPath);
        Array.Sort(paths, StringComparer.Ordinal);
        foreach (string path in paths)
        {
            string scenePath = path;
            var caption = new GUIContent(Label(path) + " (" + Path.GetFileNameWithoutExtension(path) + ")");
            menu.AddItem(caption, SceneManager.GetActiveScene().path == path, () => Open(scenePath));
        }
        if (paths.Length == 0) menu.AddDisabledItem(new GUIContent("Assets/Scenes içinde sahne yok"));
        menu.ShowAsContext();
    }

    static void Open(string path)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path == path) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Refresh();
    }

    [MenuItem("TCG Card Chaos/Scenes/Ana Menü")]
    static void MenuScene() => Open("Assets/Scenes/MenuScene.unity");
    [MenuItem("TCG Card Chaos/Scenes/Oyun")]
    static void MainScene() => Open("Assets/Scenes/MainScene.unity");
    [MenuItem("TCG Card Chaos/Scenes/Başlangıç")]
    static void BootScene() => Open("Assets/Scenes/BootScene.unity");
}

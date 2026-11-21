using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Editor-only scene shortcuts. No player/build code or scene changes.</summary>
[InitializeOnLoad]
public static class GameSceneToolbar
{
    const string ToolbarPath = "TCG Card Chaos/Sahneler";

    static GameSceneToolbar()
    {
        EditorApplication.playModeStateChanged += _ => Refresh();
        EditorSceneManager.activeSceneChangedInEditMode += (_, __) => Refresh();
    }

    [MainToolbarElement(ToolbarPath, defaultDockPosition = MainToolbarDockPosition.Left)]
    static MainToolbarElement CreateSceneDropdown()
    {
        bool ready = !EditorApplication.isPlayingOrWillChangePlaymode;
        var content = new MainToolbarContent("Sahneler: " + Label(SceneManager.GetActiveScene().path),
            ready ? "Oyunun sahneleri arasında geçiş yap" : "Sahne değiştirmek için Play modunu durdur.");
        return new MainToolbarDropdown(content, ShowScenes) { enabled = ready };
    }

    static void Refresh()
    {
        MainToolbar.Refresh(ToolbarPath);
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

    static void ShowScenes(Rect dropDownRect)
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
        menu.DropDown(dropDownRect);
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

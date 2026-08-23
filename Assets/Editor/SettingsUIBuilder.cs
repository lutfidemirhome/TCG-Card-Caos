using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persists Panel_Settings on the open menu / pause canvas so it is editable in Hierarchy.
/// Does not rebuild the pause menu or HUD.
/// </summary>
[InitializeOnLoad]
public static class SettingsUIBuilder
{
    const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
    const string GameScenePath = "Assets/Scenes/MainScene.unity";
    const string MenuCanvasName = "MainMenuCanvas";
    const string PauseCanvasName = "InGamePauseCanvas";
    const string PanelName = "Panel_Settings";

    static SettingsUIBuilder()
    {
        EditorApplication.delayCall += EnsureInOpenScenes;
    }

    static void EnsureInOpenScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EnsureArtFolder();
        GameObject created = null;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            GameObject panel = EnsureInLoadedScene(scene);
            if (panel != null)
                created = panel;
        }

        if (created == null)
            return;

        Selection.activeGameObject = created;
        EditorGUIUtility.PingObject(created);
        Debug.Log(
            "[SettingsUIBuilder] Hierarchy → MainMenuCanvas / Panel_Settings "
            + "(göz kapalı). Açıp düzenle, sonra Cmd+S.");
    }

    [MenuItem("TCG Card Caos/UI/Add Settings Panel")]
    public static void AddSettingsPanel()
    {
        if (!File.Exists(MenuScenePath))
        {
            EditorUtility.DisplayDialog("Settings", "MenuScene not found.", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        MainMenuUIBuilder.EnsureLocalizationTable();
        EnsureArtFolder();

        AddAndSave(MenuScenePath, MenuCanvasName, typeof(MainMenuView));
        if (File.Exists(GameScenePath))
            AddAndSave(GameScenePath, PauseCanvasName, typeof(InGamePauseView));

        Scene menuScene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        GameObject selected = FindInScene(menuScene, MenuCanvasName, PanelName);
        if (selected != null)
        {
            Selection.activeGameObject = selected;
            EditorGUIUtility.PingObject(selected);
        }

        EditorUtility.DisplayDialog(
            "Settings",
            "Panel_Settings Hierarchy'de: MainMenuCanvas / Panel_Settings\n"
            + "(göz kapalı — açıp düzenle).",
            "OK");
    }

    static void AddAndSave(string scenePath, string canvasName, System.Type viewType)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        EnsureOnCanvas(FindInSceneTransform(scene, canvasName), viewType, scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
    }

    static GameObject EnsureInLoadedScene(Scene scene)
    {
        if (scene.path == MenuScenePath || scene.name == "MenuScene")
            return EnsureOnCanvas(FindInSceneTransform(scene, MenuCanvasName), typeof(MainMenuView), scene);

        if (scene.path == GameScenePath || scene.name == "MainScene")
            return EnsureOnCanvas(FindInSceneTransform(scene, PauseCanvasName), typeof(InGamePauseView), scene);

        return null;
    }

    static GameObject EnsureOnCanvas(Transform canvas, System.Type viewType, Scene scene)
    {
        if (canvas == null)
            return null;

        Transform existing = canvas.Find(PanelName);
        SettingsPanelView view;

        if (existing != null)
        {
            view = existing.GetComponent<SettingsPanelView>();
            if (view == null)
                view = existing.gameObject.AddComponent<SettingsPanelView>();

            if (existing.Find("Panel") != null && !NeedsRebuild(existing))
            {
                view.ApplyControlTextMaterials();
                view.FillPreviewValues();
                AssignHost(canvas, viewType, view);
                EditorSceneManager.MarkSceneDirty(scene);
                return null;
            }
        }
        else
        {
            var go = new GameObject(PanelName, typeof(RectTransform), typeof(SettingsPanelView));
            go.transform.SetParent(canvas, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            existing = go.transform;
            view = go.GetComponent<SettingsPanelView>();
        }

        view.BuildInEditor();
        view.ApplyControlTextMaterials();
        view.FillPreviewValues();
        existing.gameObject.SetActive(true);
        existing.SetAsLastSibling();
        AssignHost(canvas, viewType, view);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path))
            EditorSceneManager.SaveScene(scene, scene.path);

        Debug.Log(
            "[SettingsUIBuilder] Hierarchy: MainMenuCanvas / Panel_Settings / Panel / Content / "
            + "Row_Language / Dropdown / Value  (sağ metinler).");

        Transform value = existing.Find("Panel/Content/Row_Language/Dropdown/Value");
        return value != null ? value.gameObject : existing.gameObject;
    }

    static bool NeedsRebuild(Transform root)
    {
        return root.Find("Dim") != null
            || root.Find("Panel/Rows") != null;
    }

    static void AssignHost(Transform canvas, System.Type viewType, SettingsPanelView view)
    {
        Component host = canvas.GetComponent(viewType);
        if (host == null)
            return;

        var serialized = new SerializedObject(host);
        SerializedProperty property = serialized.FindProperty("settingsPanel");
        if (property == null || property.objectReferenceValue == view)
            return;

        property.objectReferenceValue = view;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static Transform FindInSceneTransform(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == name)
                return roots[i].transform;

            Transform nested = FindRecursive(roots[i].transform, name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    static Transform FindRecursive(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child;

            Transform nested = FindRecursive(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    static GameObject FindInScene(Scene scene, string canvasName, string childName)
    {
        Transform canvas = FindInSceneTransform(scene, canvasName);
        if (canvas == null)
            return null;
        Transform child = canvas.Find(childName);
        return child != null ? child.gameObject : null;
    }

    static void EnsureArtFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/UI"))
            AssetDatabase.CreateFolder("Assets", "UI");
        if (!AssetDatabase.IsValidFolder("Assets/UI/Settings"))
            AssetDatabase.CreateFolder("Assets/UI", "Settings");
        if (!AssetDatabase.IsValidFolder("Assets/UI/Settings/Art"))
            AssetDatabase.CreateFolder("Assets/UI/Settings", "Art");
    }
}

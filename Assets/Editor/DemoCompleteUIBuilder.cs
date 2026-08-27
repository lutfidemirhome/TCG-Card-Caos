using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Wires existing Panel_DemoComplete. MenuScene Hierarchy is the source of truth — this never
/// rebuilds children. Runtime clones MenuScene's panel itself and does not call this.
/// </summary>
public static class DemoCompleteUIBuilder
{
    const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
    const string GameScenePath = "Assets/Scenes/MainScene.unity";
    const string MenuCanvasName = "MainMenuCanvas";
    const string PauseCanvasName = "InGamePauseCanvas";

    [MenuItem("TCG Card Chaos/UI/Add Demo Complete Popup")]
    public static void AddDemoCompletePopup()
    {
        if (!File.Exists(MenuScenePath))
        {
            EditorUtility.DisplayDialog("Demo Complete", "MenuScene not found.", "OK");
            return;
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene menuScene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        Transform menuCanvas = FindInSceneTransform(menuScene, MenuCanvasName);
        Transform menuPanel = menuCanvas != null ? menuCanvas.Find(DemoCompleteView.PanelName) : null;
        if (menuPanel == null)
        {
            EditorUtility.DisplayDialog(
                "Demo Complete",
                "MainMenuCanvas / Panel_DemoComplete MenuScene'de yok.\n"
                + "Kodu ile oluşturmuyorum — Hierarchy'de senin yaptığın paneli kullanıyoruz.",
                "OK");
            return;
        }

        WireCanvas(menuCanvas, menuPanel, menuScene);
        EditorSceneManager.MarkSceneDirty(menuScene);
        EditorSceneManager.SaveScene(menuScene, MenuScenePath);

        if (File.Exists(GameScenePath))
            CopyMenuPanelToGameScene(menuPanel);

        menuScene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        GameObject selected = FindInScene(menuScene, MenuCanvasName, DemoCompleteView.PanelName);
        if (selected != null)
        {
            Selection.activeGameObject = selected;
            EditorGUIUtility.PingObject(selected);
        }

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(
                "Demo Complete",
                "Kaynak: MenuScene / MainMenuCanvas / Panel_DemoComplete\n"
                + "Düzenlemeyi MenuScene'de yap; oyun o hali çeker.",
                "OK");
        }
    }

    static void CopyMenuPanelToGameScene(Transform menuPanel)
    {
        Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
        Transform pauseCanvas = FindInSceneTransform(game, PauseCanvasName);
        if (pauseCanvas == null)
        {
            EditorSceneManager.CloseScene(game, true);
            Debug.LogError("[DemoCompleteUIBuilder] InGamePauseCanvas not found in MainScene.");
            return;
        }

        Transform old = pauseCanvas.Find(DemoCompleteView.PanelName);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        GameObject clone = Object.Instantiate(menuPanel.gameObject, pauseCanvas, false);
        clone.name = DemoCompleteView.PanelName;
        clone.SetActive(false);
        EditorSceneManager.MoveGameObjectToScene(clone, game);

        WireCanvas(pauseCanvas, clone.transform, game);
        EditorSceneManager.MarkSceneDirty(game);
        EditorSceneManager.SaveScene(game, GameScenePath);
        EditorSceneManager.CloseScene(game, true);
    }

    static void WireCanvas(Transform canvas, Transform panel, Scene scene)
    {
        DemoCompleteView view = canvas.GetComponent<DemoCompleteView>();
        if (view == null)
            view = canvas.gameObject.AddComponent<DemoCompleteView>();

        WireSerialized(view, panel);
        panel.gameObject.SetActive(false);
        panel.SetAsLastSibling();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    static void WireSerialized(DemoCompleteView view, Transform panel)
    {
        var serialized = new SerializedObject(view);
        SerializedProperty root = serialized.FindProperty("root");
        if (root != null)
            root.objectReferenceValue = panel.gameObject;

        Transform wishlist = panel.Find("Button_Wishlist");
        SerializedProperty wishlistProp = serialized.FindProperty("wishlistButton");
        if (wishlistProp != null && wishlist != null)
            wishlistProp.objectReferenceValue = wishlist.GetComponent<Button>();

        Transform back = panel.Find("Button_Back");
        SerializedProperty backProp = serialized.FindProperty("backButton");
        if (backProp != null && back != null)
            backProp.objectReferenceValue = back.GetComponent<Button>();

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

    static GameObject FindInScene(Scene scene, string canvasName, string panelName)
    {
        Transform canvas = FindInSceneTransform(scene, canvasName);
        if (canvas == null)
            return null;

        Transform panel = canvas.Find(panelName);
        return panel != null ? panel.gameObject : null;
    }
}

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Adds Panel_SaveGame under InGamePauseCanvas. Does not rebuild the pause menu.
/// Menu: TCG Card Caos → UI → Add Save Game Panel
/// </summary>
public static class SaveGameUIBuilder
{
    const string GameScenePath = "Assets/Scenes/MainScene.unity";
    const string CanvasRootName = "InGamePauseCanvas";

    [MenuItem("TCG Card Caos/UI/Add Save Game Panel")]
    public static void AddSaveGamePanel()
    {
        if (!File.Exists(GameScenePath))
        {
            EditorUtility.DisplayDialog("Save Game", "MainScene not found.", "OK");
            return;
        }

        MainMenuUIBuilder.EnsureLocalizationTable();
        EnsureArtFolder();

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != GameScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        }

        Transform canvas = FindPauseCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog(
                "Save Game",
                "InGamePauseCanvas not found in MainScene.",
                "OK");
            return;
        }

        Transform existing = canvas.Find("Panel_SaveGame");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        LoadGamePanelView loadPanel = canvas.GetComponentInChildren<LoadGamePanelView>(true);
        if (loadPanel == null)
        {
            EditorUtility.DisplayDialog(
                "Save Game",
                "Panel_LoadGame not found. Save Game copies that layout.",
                "OK");
            return;
        }

        SaveGamePanelView savePanel = SaveGamePanelView.CreateFromLoadPanel(loadPanel, canvas);
        if (savePanel == null)
        {
            Debug.LogError("[SaveGameUIBuilder] Could not clone Panel_LoadGame.");
            return;
        }

        savePanel.transform.SetAsLastSibling();
        savePanel.gameObject.SetActive(false);

        var pauseView = canvas.GetComponent<InGamePauseView>();
        if (pauseView != null)
        {
            var serialized = new SerializedObject(pauseView);
            serialized.FindProperty("saveGamePanel").objectReferenceValue = savePanel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, GameScenePath);
        Selection.activeGameObject = savePanel.gameObject;
        EditorGUIUtility.PingObject(savePanel.gameObject);
        Debug.Log(
            "[SaveGameUIBuilder] Panel_SaveGame added under InGamePauseCanvas. "
            + "Title is TMP text. Empty-slot thumb is image_save_game.png.");
    }

    static Transform FindPauseCanvas()
    {
        GameObject found = GameObject.Find(CanvasRootName);
        return found != null ? found.transform : null;
    }

    static void EnsureArtFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/UI"))
            AssetDatabase.CreateFolder("Assets", "UI");
        if (!AssetDatabase.IsValidFolder("Assets/UI/SaveGame"))
            AssetDatabase.CreateFolder("Assets/UI", "SaveGame");
        if (!AssetDatabase.IsValidFolder("Assets/UI/SaveGame/Art"))
            AssetDatabase.CreateFolder("Assets/UI/SaveGame", "Art");
    }
}

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Resaves MainScene as Force Text YAML so the editor can open the recovered binary backup.
/// </summary>
public static class ConvertMainSceneToText
{
    const string ScenePath = "Assets/Scenes/MainScene.unity";

    [MenuItem("TCG Card Caos/Convert MainScene To Text")]
    public static void Convert()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            Debug.LogError("[ConvertMainSceneToText] Save failed.");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[ConvertMainSceneToText] MainScene saved as text.");
        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }
}
#endif

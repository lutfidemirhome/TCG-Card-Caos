#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class MainSceneShortcut : ScriptableObject
{
}

[CustomEditor(typeof(MainSceneShortcut))]
public class MainSceneShortcutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Kendi sahnene donmek icin cift tikla veya asagidaki butonu kullan.",
            MessageType.Info);

        if (GUILayout.Button("Main Scene'i Ac", GUILayout.Height(32)))
            FirstPersonSceneSetup.OpenMainScene();
    }
}

public static class MainSceneShortcutOpener
{
    [UnityEditor.Callbacks.OnOpenAsset(1)]
    public static bool OpenShortcut(int instanceID, int line)
    {
        if (EditorUtility.InstanceIDToObject(instanceID) is not MainSceneShortcut)
            return false;

        FirstPersonSceneSetup.OpenMainScene();
        return true;
    }
}
#endif

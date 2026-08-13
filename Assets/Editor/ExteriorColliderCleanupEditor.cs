using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExteriorColliderCleanupEditor
{
    const string ScenePath = "Assets/Scenes/MainScene.unity";
    const string AeNewYorkRoot = "Assets/AE_New_York/";

    [MenuItem("TCG Card Caos/Strip Exterior Colliders")]
    public static void StripExteriorCollidersInMainScene()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        int removedColliders = StripComponents<Collider>();
        int removedRigidbodies = StripComponents<Rigidbody>();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "Exterior collider temizligi",
            $"Kaldirilan collider: {removedColliders}\n"
            + $"Kaldirilan rigidbody: {removedRigidbodies}\n\n"
            + "Room, duvarlar ve Door_c5bu08 dokunulmadi.",
            "Tamam");
    }

    static int StripComponents<T>() where T : Component
    {
        T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int removed = 0;

        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null || !ShouldStripInEditor(component.gameObject))
                continue;

            Undo.DestroyObjectImmediate(component);
            removed++;
        }

        return removed;
    }

    static bool ShouldStripInEditor(GameObject gameObject)
    {
        if (ExteriorColliderCleanup.IsProtectedTransform(gameObject.transform))
            return false;

        if (ExteriorColliderCleanup.IsExteriorObject(gameObject))
            return true;

        GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
        if (prefabRoot == null)
            return false;

        if (ExteriorColliderCleanup.IsProtectedTransform(prefabRoot.transform))
            return false;

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabRoot);
        return !string.IsNullOrEmpty(prefabPath) && prefabPath.StartsWith(AeNewYorkRoot);
    }
}

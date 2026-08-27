using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExteriorColliderCleanupEditor
{
    const string ScenePath = "Assets/Scenes/MainScene.unity";
    const string AeNewYorkRoot = "Assets/AE_New_York/";

    [MenuItem("TCG Card Chaos/Strip Exterior Colliders")]
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
            + "Room, shop duvarlari (Wall_*) ve Door_c5bu08 dokunulmadi.\n"
            + "House_*_Wall_* ic duvar collider'lari korundu.\n"
            + "Column_* kolon collider'lari korundu.",
            "Tamam");
    }

    [MenuItem("TCG Card Chaos/Ensure Column Colliders")]
    public static void EnsureColumnCollidersInMainScene()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        int added = ExteriorColliderCleanup.EnsurePlacedColumnColliders();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "Kolon collider",
            added > 0
                ? $"Column objelerine {added} collider eklendi."
                : "Column objelerinde collider zaten var veya Column bulunamadi.",
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

        if (ExteriorColliderCleanup.IsPlacedHouseWallTransform(gameObject.transform))
            return false;

        if (ExteriorColliderCleanup.IsPlacedColumnTransform(gameObject.transform))
            return false;

        if (ExteriorColliderCleanup.IsExteriorObject(gameObject))
            return true;

        GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
        if (prefabRoot == null)
            return false;

        if (ExteriorColliderCleanup.IsProtectedTransform(prefabRoot.transform))
            return false;

        if (ExteriorColliderCleanup.IsPlacedColumnTransform(prefabRoot.transform))
            return false;

        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabRoot);
        return !string.IsNullOrEmpty(prefabPath) && prefabPath.StartsWith(AeNewYorkRoot);
    }
}

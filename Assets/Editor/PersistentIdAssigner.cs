using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PersistentIdAssigner
{
    [MenuItem("TCG Card Chaos/Save/Assign Persistent IDs In Open Scenes")]
    public static void AssignInOpenScenes()
    {
        int assigned = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            assigned += AssignInScene(SceneManager.GetSceneAt(i));

        Debug.Log("[Save] Assigned or verified PersistentId on " + assigned + " scene objects.");
    }

    static int AssignInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return 0;

        int count = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            count += AssignRecursive(roots[i].transform);

        if (count > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        return count;
    }

    static int AssignRecursive(Transform transform)
    {
        int count = 0;
        if (transform.GetComponent<CardShelf>() != null
            || transform.GetComponent<PsaCabinet>() != null)
        {
            PersistentId persistent = PersistentId.GetOrCreate(transform.gameObject);
            persistent.EnsureAssigned();
            EditorUtility.SetDirty(persistent);
            count++;
        }

        for (int i = 0; i < transform.childCount; i++)
            count += AssignRecursive(transform.GetChild(i));

        return count;
    }
}

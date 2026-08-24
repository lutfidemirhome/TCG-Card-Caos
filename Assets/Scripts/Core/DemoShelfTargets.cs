using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The six Hierarchy objects that count as "demo complete". Other cabinets in MainScene are ignored.
/// </summary>
public static class DemoShelfTargets
{
    public static readonly string[] ObjectNames =
    {
        "Cabinets_FireCommon",
        "Cabinets_FireUncommon",
        "Cabinets_FireRare",
        "Cabinets_GrassRare",
        "Cabinets_GrassUncommon (1)",
        "KartTutucu_1",
    };

    public static bool AreAllComplete()
    {
        for (int i = 0; i < ObjectNames.Length; i++)
        {
            if (!IsComplete(ObjectNames[i]))
                return false;
        }

        return true;
    }

    public static bool IsComplete(string objectName)
    {
        Transform root = FindNamed(objectName);
        if (root == null)
            return false;

        CardShelf shelf = root.GetComponent<CardShelf>();
        if (shelf == null)
            shelf = root.GetComponentInChildren<CardShelf>(true);
        if (shelf != null)
            return shelf.IsComplete();

        PsaCabinet cabinet = root.GetComponent<PsaCabinet>();
        if (cabinet == null)
            cabinet = root.GetComponentInChildren<PsaCabinet>(true);
        if (cabinet != null)
            return cabinet.IsComplete();

        return false;
    }

    static Transform FindNamed(string objectName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform found = FindRecursive(roots[r].transform, objectName);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    static Transform FindRecursive(Transform parent, string objectName)
    {
        if (parent.name == objectName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform nested = FindRecursive(parent.GetChild(i), objectName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}

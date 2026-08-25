using System.Collections.Generic;
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

    static readonly HashSet<string> NameSet = new HashSet<string>(ObjectNames);
    static readonly List<CardShelf> Shelves = new List<CardShelf>(8);
    static readonly List<PsaCabinet> Cabinets = new List<PsaCabinet>(2);
    static int _cachedSceneHandle = int.MinValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        ClearCache();
    }

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

    public static void CollectProgress(out int cardsPlaced, out int shelvesCompleted, out int cabinetsCompleted)
    {
        ResolveTargets();
        cardsPlaced = 0;
        shelvesCompleted = 0;
        cabinetsCompleted = 0;

        for (int i = 0; i < Shelves.Count; i++)
        {
            CardShelf shelf = Shelves[i];
            if (shelf == null)
                continue;

            cardsPlaced += shelf.CountCorrectlyPlacedCards();
            if (shelf.IsComplete())
                shelvesCompleted++;
        }

        for (int i = 0; i < Cabinets.Count; i++)
        {
            PsaCabinet cabinet = Cabinets[i];
            if (cabinet == null)
                continue;

            cardsPlaced += cabinet.CountCorrectlyPlacedCards();
            if (cabinet.IsComplete())
            {
                shelvesCompleted++;
                cabinetsCompleted++;
            }
        }
    }

    static void ResolveTargets()
    {
        Scene active = SceneManager.GetActiveScene();
        if (_cachedSceneHandle == active.handle && (Shelves.Count > 0 || Cabinets.Count > 0))
            return;

        ClearCache();
        _cachedSceneHandle = active.handle;

        CardShelf[] shelves = Object.FindObjectsByType<CardShelf>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < shelves.Length; i++)
        {
            CardShelf shelf = shelves[i];
            if (shelf != null && IsDemoTarget(shelf.transform))
                Shelves.Add(shelf);
        }

        PsaCabinet[] cabinets = Object.FindObjectsByType<PsaCabinet>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < cabinets.Length; i++)
        {
            PsaCabinet cabinet = cabinets[i];
            if (cabinet != null && IsDemoTarget(cabinet.transform))
                Cabinets.Add(cabinet);
        }
    }

    static bool IsDemoTarget(Transform transform)
    {
        while (transform != null)
        {
            if (NameSet.Contains(transform.name))
                return true;

            transform = transform.parent;
        }

        return false;
    }

    static void ClearCache()
    {
        Shelves.Clear();
        Cabinets.Clear();
        _cachedSceneHandle = int.MinValue;
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

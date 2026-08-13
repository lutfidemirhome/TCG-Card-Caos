using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Removes physics colliders from decorative exterior props (houses, roads, traffic cars).
/// Shop geometry and Door_c5bu08 are always left untouched.
/// </summary>
public static class ExteriorColliderCleanup
{
    const string MainSceneName = "MainScene";

    static readonly string[] ProtectedRootNames =
    {
        "Room",
        "Player",
        "Door_c5bu08",
    };

    static readonly string[] ProtectedNamePrefixes =
    {
        "Wall_",
        "Shelf",
        "Cabinet",
        "Card",
        "Floor",
        "Ceiling",
    };

    static readonly string[] ExteriorRootPrefixes =
    {
        "House_",
        "Optimized_Scene",
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterSceneLoad()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != MainSceneName)
            return;

        ApplyRuntime();
    }

    public static void ApplyRuntime()
    {
        RemoveMatchingComponents<Collider>();
        RemoveMatchingComponents<Rigidbody>();
        EnsurePlacedHouseWallColliders();
    }

    public static bool ShouldStrip(GameObject gameObject)
    {
        if (gameObject == null)
            return false;

        if (IsProtectedTransform(gameObject.transform))
            return false;

        if (IsPlacedHouseWallTransform(gameObject.transform))
            return false;

        return IsExteriorObject(gameObject);
    }

    public static bool IsPlacedHouseWallName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        if (!objectName.StartsWith("House_", StringComparison.OrdinalIgnoreCase))
            return false;

        return objectName.IndexOf("_Wall_", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsPlacedHouseWallTransform(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            if (IsPlacedHouseWallName(current.name))
                return true;

            current = current.parent;
        }

        return false;
    }

    public static void EnsurePlacedHouseWallColliders()
    {
        MeshRenderer[] renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null || IsProtectedTransform(renderer.transform))
                continue;

            if (!IsPlacedHouseWallTransform(renderer.transform))
                continue;

            if (renderer.GetComponent<Collider>() != null)
                continue;

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;

            BoxCollider boxCollider = renderer.gameObject.AddComponent<BoxCollider>();
            Bounds bounds = meshFilter.sharedMesh.bounds;
            boxCollider.center = bounds.center;
            boxCollider.size = bounds.size;
        }
    }

    public static bool IsProtectedTransform(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            string name = current.name;
            for (int i = 0; i < ProtectedRootNames.Length; i++)
            {
                if (name == ProtectedRootNames[i])
                    return true;
            }

            for (int i = 0; i < ProtectedNamePrefixes.Length; i++)
            {
                if (name.StartsWith(ProtectedNamePrefixes[i]))
                    return true;
            }

            current = current.parent;
        }

        return false;
    }

    public static bool IsExteriorObject(GameObject gameObject)
    {
        if (IsExteriorRootName(GetOutermostRoot(gameObject.transform).name))
            return true;

        Transform current = gameObject.transform;
        while (current != null)
        {
            if (current.name == "ExteriorTraffic")
                return true;

            current = current.parent;
        }

        return false;
    }

    static bool IsExteriorRootName(string rootName)
    {
        for (int i = 0; i < ExteriorRootPrefixes.Length; i++)
        {
            if (rootName.StartsWith(ExteriorRootPrefixes[i]))
                return true;
        }

        return false;
    }

    static Transform GetOutermostRoot(Transform transform)
    {
        Transform root = transform;
        while (root.parent != null)
            root = root.parent;

        return root;
    }

    static void RemoveMatchingComponents<T>() where T : Component
    {
        T[] components = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null || !ShouldStrip(component.gameObject))
                continue;

            UnityEngine.Object.Destroy(component);
        }
    }
}

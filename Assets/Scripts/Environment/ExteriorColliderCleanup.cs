using System;
using System.Collections.Generic;
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
        EnsurePlacedColumnColliders();
    }

    public static bool ShouldStrip(GameObject gameObject)
    {
        if (gameObject == null)
            return false;

        if (IsProtectedTransform(gameObject.transform))
            return false;

        if (IsPlacedHouseWallTransform(gameObject.transform))
            return false;

        if (IsPlacedColumnTransform(gameObject.transform))
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

    public static bool IsPlacedColumnName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        return objectName.StartsWith("Column", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPlacedColumnTransform(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            if (IsPlacedColumnName(current.name))
                return true;

            current = current.parent;
        }

        return false;
    }

    public static int EnsurePlacedColumnColliders()
    {
        var processedRoots = new HashSet<int>();
        int added = 0;

        MeshRenderer[] renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Transform columnRoot = FindColumnRoot(renderer.transform);
            if (columnRoot == null || IsProtectedTransform(columnRoot))
                continue;

            if (!processedRoots.Add(columnRoot.GetInstanceID()))
                continue;

            if (columnRoot.GetComponent<Collider>() != null)
                continue;

            if (TryAddBoxColliderFromRenderers(columnRoot))
                added++;
        }

        return added;
    }

    static Transform FindColumnRoot(Transform transform)
    {
        Transform columnRoot = null;
        Transform current = transform;
        while (current != null)
        {
            if (IsPlacedColumnName(current.name))
                columnRoot = current;

            current = current.parent;
        }

        return columnRoot;
    }

    static bool TryAddBoxColliderFromRenderers(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return false;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        BoxCollider boxCollider = root.gameObject.AddComponent<BoxCollider>();
        Vector3 localSize = root.InverseTransformVector(bounds.size);
        boxCollider.center = root.InverseTransformPoint(bounds.center);
        boxCollider.size = new Vector3(
            Mathf.Abs(localSize.x),
            Mathf.Abs(localSize.y),
            Mathf.Abs(localSize.z));
        return true;
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

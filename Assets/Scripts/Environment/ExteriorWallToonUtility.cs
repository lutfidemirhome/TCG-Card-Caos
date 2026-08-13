using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies the shop Wall.mat toon edge shading to exterior house wall meshes.
/// </summary>
public static class ExteriorWallToonUtility
{
    const string WallTemplatePath = "Assets/Art/Materials/Wall.mat";

    static readonly string[] ProtectedRootNames =
    {
        "Room",
        "Player",
        "Door_c5bu08",
    };

    static readonly string[] ProtectedNamePrefixes =
    {
        "Wall_",
    };

    public static int ApplyAll(Material wallTemplate, bool useSharedMaterials = true)
    {
        if (wallTemplate == null)
            return 0;

        var processedRenderers = new HashSet<int>();
        int changedMaterialSlots = 0;

        MeshRenderer[] renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null || !ShouldApplyToRenderer(renderer))
                continue;

            if (!processedRenderers.Add(renderer.GetInstanceID()))
                continue;

            changedMaterialSlots += ApplyToRenderer(renderer, wallTemplate, useSharedMaterials);
        }

        return changedMaterialSlots;
    }

    public static int ApplyToRenderer(MeshRenderer renderer, Material wallTemplate, bool useSharedMaterials)
    {
        if (renderer == null || wallTemplate == null)
            return 0;

        Material[] sourceMaterials = useSharedMaterials ? renderer.sharedMaterials : renderer.materials;
        if (sourceMaterials == null || sourceMaterials.Length == 0)
            return 0;

        int changed = 0;
        Material[] targetMaterials = new Material[sourceMaterials.Length];
        for (int i = 0; i < sourceMaterials.Length; i++)
        {
            Material source = sourceMaterials[i];
            if (source == null || AlreadyUsesTemplate(source, wallTemplate))
            {
                targetMaterials[i] = source;
                continue;
            }

            targetMaterials[i] = CreateToonMaterial(source, wallTemplate);
            changed++;
        }

        if (changed == 0)
            return 0;

        if (useSharedMaterials)
            renderer.sharedMaterials = targetMaterials;
        else
            renderer.materials = targetMaterials;

        return changed;
    }

    public static Material CreateToonMaterial(Material source, Material wallTemplate)
    {
        Material toonMaterial = new Material(wallTemplate)
        {
            name = source.name + "_Toon"
        };

        CopyTexture(source, toonMaterial, "_BaseMap");
        CopyTexture(source, toonMaterial, "_MainTex");
        CopyTexture(source, toonMaterial, "_BumpMap");
        CopyTexture(source, toonMaterial, "_MetallicGlossMap");
        CopyTexture(source, toonMaterial, "_OcclusionMap");
        CopyColor(source, toonMaterial, "_BaseColor");
        CopyColor(source, toonMaterial, "_Color");

        return toonMaterial;
    }

    public static bool ShouldApplyToRenderer(Renderer renderer)
    {
        if (renderer == null || IsProtectedTransform(renderer.transform))
            return false;

        return IsExteriorHouseWallName(renderer.gameObject.name)
            || HasExteriorHouseWallAncestor(renderer.transform);
    }

    public static bool IsExteriorHouseWallName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        if (!objectName.StartsWith("House_", StringComparison.OrdinalIgnoreCase))
            return false;

        return objectName.IndexOf("_Wall_", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool HasExteriorHouseWallAncestor(Transform transform)
    {
        Transform current = transform.parent;
        while (current != null)
        {
            if (IsExteriorHouseWallName(current.name))
                return true;

            current = current.parent;
        }

        return false;
    }

    public static bool IsProtectedTransform(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            string name = current.name;
            for (int i = 0; i < ProtectedRootNames.Length; i++)
            {
                if (string.Equals(name, ProtectedRootNames[i], StringComparison.Ordinal))
                    return true;
            }

            for (int i = 0; i < ProtectedNamePrefixes.Length; i++)
            {
                if (name.StartsWith(ProtectedNamePrefixes[i], StringComparison.Ordinal))
                    return true;
            }

            current = current.parent;
        }

        return false;
    }

    static bool AlreadyUsesTemplate(Material material, Material wallTemplate)
    {
        return material.shader == wallTemplate.shader;
    }

    static void CopyTexture(Material source, Material destination, string propertyName)
    {
        if (!source.HasProperty(propertyName) || !destination.HasProperty(propertyName))
            return;

        Texture texture = source.GetTexture(propertyName);
        if (texture == null)
            return;

        destination.SetTexture(propertyName, texture);
    }

    static void CopyColor(Material source, Material destination, string propertyName)
    {
        if (!source.HasProperty(propertyName) || !destination.HasProperty(propertyName))
            return;

        destination.SetColor(propertyName, source.GetColor(propertyName));
    }

    public static Material LoadWallTemplate()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(WallTemplatePath);
#else
        MeshRenderer roomWall = FindRoomWallRenderer();
        return roomWall != null ? roomWall.sharedMaterial : null;
#endif
    }

    static MeshRenderer FindRoomWallRenderer()
    {
        GameObject wall = GameObject.Find("Wall_North");
        if (wall == null)
            wall = GameObject.Find("Wall_South");
        if (wall == null)
            return null;

        return wall.GetComponent<MeshRenderer>();
    }
}

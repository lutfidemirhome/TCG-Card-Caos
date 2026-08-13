using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies the shop Wall.mat toon edge shading to exterior house wall meshes.
/// </summary>
public static class ExteriorWallToonUtility
{
    const string WallTemplatePath = "Assets/Art/Materials/Wall.mat";
    const string HiddenSubmeshShaderName = "TCG/HiddenSubmesh";

    static Material _runtimeHiddenBackingMaterial;

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

            changedMaterialSlots += HideBackingLayersOnRenderer(renderer, useSharedMaterials);

            if (!RendererNeedsToonConversion(renderer, wallTemplate))
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
            if (source == null)
            {
                targetMaterials[i] = source;
                continue;
            }

            if (IsBrickBackingMaterial(source))
            {
                targetMaterials[i] = source;
                continue;
            }

            if (IsPlinthMaterial(source))
            {
                if (AlreadyUsesPlinthStyle(source, wallTemplate))
                {
                    targetMaterials[i] = source;
                    continue;
                }

                targetMaterials[i] = CreatePlinthToonMaterial(wallTemplate, source);
                changed++;
                continue;
            }

            if (AlreadyUsesTemplate(source, wallTemplate))
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

    public static Material CreatePlinthToonMaterial(Material wallTemplate, Material source = null)
    {
        Material plinthMaterial = new Material(wallTemplate)
        {
            name = source != null && !string.IsNullOrEmpty(source.name)
                ? source.name
                : "Plinths_Toon"
        };

        ClearTexture(plinthMaterial, "_BaseMap");
        ClearTexture(plinthMaterial, "_MainTex");
        ClearTexture(plinthMaterial, "_BumpMap");
        ClearTexture(plinthMaterial, "_MetallicGlossMap");
        ClearTexture(plinthMaterial, "_OcclusionMap");

        Color baseColor = Color.black;
        Color shadowColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        Color highlightColor = new Color(0.22f, 0.22f, 0.22f, 1f);

        if (source != null && source.shader == wallTemplate.shader)
        {
            CopyColor(source, plinthMaterial, "_BaseColor");
            CopyColor(source, plinthMaterial, "_Color");
            CopyColor(source, plinthMaterial, "_SColor");
            CopyColor(source, plinthMaterial, "_HColor");
        }
        else
        {
            plinthMaterial.SetColor("_BaseColor", baseColor);
            plinthMaterial.SetColor("_Color", baseColor);

            if (plinthMaterial.HasProperty("_SColor"))
                plinthMaterial.SetColor("_SColor", shadowColor);

            if (plinthMaterial.HasProperty("_HColor"))
                plinthMaterial.SetColor("_HColor", highlightColor);
        }

        return plinthMaterial;
    }

    public static bool RendererNeedsToonConversion(MeshRenderer renderer, Material wallTemplate)
    {
        if (renderer == null || wallTemplate == null)
            return false;

        Material[] materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
            return false;

        for (int i = 0; i < materials.Length; i++)
        {
            Material source = materials[i];
            if (source == null)
                continue;

            if (IsBrickBackingMaterial(source))
                continue;

            if (IsPlinthMaterial(source))
            {
                if (!AlreadyUsesPlinthStyle(source, wallTemplate))
                    return true;

                continue;
            }

            if (!AlreadyUsesTemplate(source, wallTemplate))
                return true;
        }

        return false;
    }

    public static bool IsPlinthMaterial(Material material)
    {
        if (material == null || string.IsNullOrEmpty(material.name))
            return false;

        return material.name.IndexOf("Plinth", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsBrickBackingMaterial(Material material)
    {
        if (material == null || string.IsNullOrEmpty(material.name))
            return false;

        if (IsPlinthMaterial(material))
            return false;

        if (material.name.IndexOf("Wallpaper", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        if (material.name.IndexOf("Hidden", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        string materialName = material.name.Replace("_Toon", string.Empty);
        if (!materialName.StartsWith("House_", StringComparison.OrdinalIgnoreCase))
            return false;

        return materialName.IndexOf("_Wall_", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static int HideBackingLayersOnRenderer(MeshRenderer renderer, bool useSharedMaterials)
    {
        if (renderer == null)
            return 0;

        Material hiddenMaterial = LoadHiddenBackingMaterial();
        if (hiddenMaterial == null)
            return 0;

        Material[] sourceMaterials = useSharedMaterials ? renderer.sharedMaterials : renderer.materials;
        if (sourceMaterials == null || sourceMaterials.Length == 0)
            return 0;

        int changed = 0;
        Material[] targetMaterials = new Material[sourceMaterials.Length];
        for (int i = 0; i < sourceMaterials.Length; i++)
        {
            Material source = sourceMaterials[i];
            if (source != null
                && IsBrickBackingMaterial(source)
                && source.shader != hiddenMaterial.shader)
            {
                targetMaterials[i] = hiddenMaterial;
                changed++;
                continue;
            }

            targetMaterials[i] = source;
        }

        if (changed == 0)
            return 0;

        if (useSharedMaterials)
            renderer.sharedMaterials = targetMaterials;
        else
            renderer.materials = targetMaterials;

        return changed;
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

    static bool AlreadyUsesPlinthStyle(Material material, Material wallTemplate)
    {
        if (material.shader != wallTemplate.shader || !IsPlinthMaterial(material))
            return false;

        if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
            return false;

        return true;
    }

    static void ClearTexture(Material material, string propertyName)
    {
        if (!material.HasProperty(propertyName))
            return;

        material.SetTexture(propertyName, null);
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

    public static Material LoadHiddenBackingMaterial()
    {
        if (_runtimeHiddenBackingMaterial != null)
            return _runtimeHiddenBackingMaterial;

        Shader shader = Shader.Find(HiddenSubmeshShaderName);
        if (shader == null)
            return null;

        _runtimeHiddenBackingMaterial = new Material(shader)
        {
            name = "InteriorWallBackingHidden",
            hideFlags = HideFlags.HideAndDontSave
        };
        return _runtimeHiddenBackingMaterial;
    }

    public static int HideAllPlacedHouseWallBacking(bool useSharedMaterials = true)
    {
        if (LoadHiddenBackingMaterial() == null)
            return 0;

        int changedMaterialSlots = 0;
        MeshRenderer[] renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null || !ShouldApplyToRenderer(renderer))
                continue;

            changedMaterialSlots += HideBackingLayersOnRenderer(renderer, useSharedMaterials);
        }

        return changedMaterialSlots;
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

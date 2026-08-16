using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Loads booster pack PBR textures from Resources and applies them to imported pack meshes.
/// Each variant lives under Resources/Cards/BoosterPack/Pack01 … Pack05.
/// </summary>
public static class PackArtLibrary
{
    public const int PackVariantCount = 5;

    const string LegacyWorldMaterialResourcePath = "Cards/BoosterPack/BoosterPackWorld";
    const string LegacyBaseColorResourcePath = "Cards/BoosterPack/cards_DefaultMaterial_BaseColor";
    const string LegacyNormalResourcePath = "Cards/BoosterPack/cards_DefaultMaterial_Normal";
    const string LegacyMetallicResourcePath = "Cards/BoosterPack/cards_DefaultMaterial_Metallic";
    const string LegacyRoughnessResourcePath = "Cards/BoosterPack/cards_DefaultMaterial_Roughness";

    static readonly Dictionary<int, Material> WorldMaterialTemplates = new Dictionary<int, Material>(PackVariantCount);

    static readonly string[] VariantDisplayNames =
    {
        "Crystal Eclipse Pack",
        "Blazing Horizon Pack",
        "Frozen Tempest Pack",
        "Emerald Wilds Pack",
        "Solar Abyss Pack",
    };

    public static string GetVariantDisplayName(int packVariantIndex)
    {
        int index = Mathf.Clamp(packVariantIndex, 1, PackVariantCount) - 1;
        return VariantDisplayNames[index];
    }

    /// <summary>
    /// UI inspect preview art — one upright PNG per variant, separate from the 3D foil atlas.
    /// Drop files at Resources/Cards/BoosterPack/Pack0N/Pack0N_Preview.png
    /// </summary>
    public static Texture2D GetVariantPreview(int packVariantIndex)
    {
        packVariantIndex = Mathf.Clamp(packVariantIndex, 1, PackVariantCount);
        Texture2D preview = LoadVariantTexture(packVariantIndex, "Preview");
        if (preview == null && packVariantIndex != 1)
            preview = LoadVariantTexture(1, "Preview");
        return preview;
    }

    public static Texture2D GetVariantBaseColor(int packVariantIndex)
    {
        packVariantIndex = Mathf.Clamp(packVariantIndex, 1, PackVariantCount);
        Texture2D baseColor = LoadVariantTexture(packVariantIndex, "BaseColor");
        if (baseColor == null && packVariantIndex != 1)
            baseColor = LoadVariantTexture(1, "BaseColor");
        if (baseColor == null)
            baseColor = Resources.Load<Texture2D>(LegacyBaseColorResourcePath);
        return baseColor;
    }

    public static void ApplyPackMaterials(Renderer renderer, int packVariantIndex = 1)
    {
        if (renderer == null)
            return;

        Material template = GetWorldMaterialTemplate(packVariantIndex);
        if (template == null)
            return;

        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
            materials[i] = template;

        renderer.sharedMaterials = materials;
    }

    public static void ApplyPackMaterials(Transform visualRoot, int packVariantIndex = 1)
    {
        if (visualRoot == null)
            return;

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            ApplyPackMaterials(renderers[i], packVariantIndex);
    }

    static Material GetWorldMaterialTemplate(int packVariantIndex)
    {
        packVariantIndex = Mathf.Clamp(packVariantIndex, 1, PackVariantCount);
        if (WorldMaterialTemplates.TryGetValue(packVariantIndex, out Material cached) && cached != null)
            return cached;

        Material created = BuildWorldMaterial(packVariantIndex);
        if (created != null)
            WorldMaterialTemplates[packVariantIndex] = created;

        return created;
    }

    static Material BuildWorldMaterial(int packVariantIndex)
    {
        string folder = GetVariantFolderResourcePath(packVariantIndex);
        string prefix = GetVariantFilePrefix(packVariantIndex);

        Material loaded = Resources.Load<Material>(folder + "/" + prefix + "World");
        if (loaded != null)
        {
            Material instance = Object.Instantiate(loaded);
            instance.name = loaded.name;
            ConfigurePackWorldMaterial(instance);
            return instance;
        }

        Texture2D baseColor = LoadVariantTexture(packVariantIndex, "BaseColor");
        Texture2D normalMap = LoadVariantTexture(packVariantIndex, "Normal");
        Texture2D metallicMap = LoadVariantTexture(packVariantIndex, "Metallic");
        Texture2D roughnessMap = LoadVariantTexture(packVariantIndex, "Roughness");

        if (baseColor == null && packVariantIndex != 1)
        {
            baseColor = LoadVariantTexture(1, "BaseColor");
            if (normalMap == null)
                normalMap = LoadVariantTexture(1, "Normal");
            if (metallicMap == null)
                metallicMap = LoadVariantTexture(1, "Metallic");
            if (roughnessMap == null)
                roughnessMap = LoadVariantTexture(1, "Roughness");
        }

        if (baseColor == null)
            baseColor = Resources.Load<Texture2D>(LegacyBaseColorResourcePath);

        if (baseColor == null)
        {
            Material legacyMaterial = Resources.Load<Material>(LegacyWorldMaterialResourcePath);
            if (legacyMaterial != null)
            {
                Material instance = Object.Instantiate(legacyMaterial);
                instance.name = legacyMaterial.name;
                ConfigurePackWorldMaterial(instance);
                return instance;
            }

            Debug.LogWarning("PackArtLibrary: No pack textures found for variant " + packVariantIndex + ".");
            return null;
        }

        if (normalMap == null)
            normalMap = Resources.Load<Texture2D>(LegacyNormalResourcePath);
        if (metallicMap == null)
            metallicMap = Resources.Load<Texture2D>(LegacyMetallicResourcePath);
        if (roughnessMap == null)
            roughnessMap = Resources.Load<Texture2D>(LegacyRoughnessResourcePath);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader) { name = prefix + "World" };
        material.SetTexture("_BaseMap", baseColor);
        material.SetTexture("_MainTex", baseColor);

        if (normalMap != null)
        {
            material.SetTexture("_BumpMap", normalMap);
            material.EnableKeyword("_NORMALMAP");
        }

        if (metallicMap != null)
        {
            material.SetTexture("_MetallicGlossMap", metallicMap);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (roughnessMap != null && material.HasProperty("_SpecGlossMap"))
            material.SetTexture("_SpecGlossMap", roughnessMap);

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallicMap != null ? 1f : 0.15f);

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", roughnessMap != null ? 1f : 0.45f);

        ConfigurePackWorldMaterial(material);
        return material;
    }

    static Texture2D LoadVariantTexture(int packVariantIndex, string mapSuffix)
    {
        string folder = GetVariantFolderResourcePath(packVariantIndex);
        string prefix = GetVariantFilePrefix(packVariantIndex);
        return Resources.Load<Texture2D>(folder + "/" + prefix + "_" + mapSuffix);
    }

    static string GetVariantFolderResourcePath(int packVariantIndex)
    {
        return "Cards/BoosterPack/Pack" + packVariantIndex.ToString("00");
    }

    static string GetVariantFilePrefix(int packVariantIndex)
    {
        return "Pack" + packVariantIndex.ToString("00");
    }

    /// <summary>
    /// PackCardRef uses <see cref="CardArtLibrary.WorldVisualScale"/> (X = -1), which mirrors
    /// child geometry. Shader U flip restores left-right correct pack art on both faces.
    /// Double-sided culling keeps front/back visible after the parent mirror.
    /// </summary>
    public static void ConfigurePackWorldMaterial(Material material)
    {
        if (material == null)
            return;

        CardArtLibrary.ConfigureGroundWorldMaterial(material);
        ApplyPackTextureUFlip(material);

        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);
    }

    static void ApplyPackTextureUFlip(Material material)
    {
        CardArtLibrary.ApplyBackTextureUFlip(material);
        ApplyTextureUFlipIfSet(material, "_BumpMap");
        ApplyTextureUFlipIfSet(material, "_MetallicGlossMap");
        ApplyTextureUFlipIfSet(material, "_SpecGlossMap");
    }

    static void ApplyTextureUFlipIfSet(Material material, string propertyName)
    {
        if (material == null || !material.HasProperty(propertyName))
            return;

        if (material.GetTexture(propertyName) == null)
            return;

        material.SetTextureScale(propertyName, CardArtLibrary.BackTextureUScale);
        material.SetTextureOffset(propertyName, CardArtLibrary.BackTextureUOffset);
    }
}

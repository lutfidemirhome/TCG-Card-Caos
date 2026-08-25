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
    const string PackToonTemplateResourcePath = "Cards/BoosterPack/PackToonTemplate";
    const string LegacyBaseColorResourcePath = "Cards/BoosterPack/cards_DefaultMaterial_BaseColor";
    const string LegacyNormalResourcePath = "Cards/BoosterPack/cards_DefaultMaterial_Normal";
    const string LegacyMetallicResourcePath = "Cards/BoosterPack/cards_DefaultMaterial_Metallic";
    const string LegacyRoughnessResourcePath = "Cards/BoosterPack/cards_DefaultMaterial_Roughness";

#if UNITY_EDITOR
    const string PackToonTemplateAssetPath = "Assets/Resources/Cards/BoosterPack/PackToonTemplate.mat";
#endif

#if UNITY_EDITOR
    [UnityEditor.InitializeOnEnterPlayMode]
    static void ResetPlayModeCaches(UnityEditor.EnterPlayModeOptions options)
    {
        WorldMaterialTemplates.Clear();
        HandMaterialTemplates.Clear();
        _packToonTemplate = null;
    }
#endif

    static readonly Dictionary<int, Material> WorldMaterialTemplates = new Dictionary<int, Material>(PackVariantCount);
    static readonly Dictionary<int, Material> HandMaterialTemplates = new Dictionary<int, Material>(PackVariantCount);
    static Material _packToonTemplate;

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

    public static void ApplyPackMaterials(Renderer renderer, int packVariantIndex = 1, bool forHand = false)
    {
        if (renderer == null)
            return;

        Material template = forHand
            ? GetHandMaterialTemplate(packVariantIndex)
            : GetWorldMaterialTemplate(packVariantIndex);
        if (template == null)
            return;

        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = template;
            if (materials[i] != null)
                materials[i].enableInstancing = false;
        }

        renderer.sharedMaterials = materials;
    }

    /// <summary>
    /// Play-mode hand tuning: unique material instances so Inspector edits show live in Game view.
    /// Discarded when Play stops or the pack returns to the world.
    /// </summary>
    public static Material[] CreatePackHandMaterialInstances(Renderer renderer, int packVariantIndex = 1)
    {
        if (renderer == null)
            return null;

        Material template = GetHandMaterialTemplate(packVariantIndex);
        if (template == null)
            return null;

        Material[] shared = renderer.sharedMaterials;
        var instances = new Material[shared.Length];
        for (int i = 0; i < instances.Length; i++)
        {
            instances[i] = new Material(template)
            {
                name = template.name + "_LiveTune"
            };
        }

        renderer.materials = instances;
        return instances;
    }

    public static void DestroyMaterialInstances(Material[] instances)
    {
        if (instances == null)
            return;

        for (int i = 0; i < instances.Length; i++)
        {
            Material material = instances[i];
            if (material == null)
                continue;

            if (Application.isPlaying)
                Object.Destroy(material);
            else
                Object.DestroyImmediate(material);
        }
    }

    public static void ApplyPackMaterials(Transform visualRoot, int packVariantIndex = 1, bool forHand = false)
    {
        if (visualRoot == null)
            return;

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            ApplyPackMaterials(renderers[i], packVariantIndex, forHand);
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

    static Material GetHandMaterialTemplate(int packVariantIndex)
    {
        packVariantIndex = Mathf.Clamp(packVariantIndex, 1, PackVariantCount);
        if (HandMaterialTemplates.TryGetValue(packVariantIndex, out Material cached) && cached != null)
            return cached;

        Material created = BuildPackMaterial(packVariantIndex, forHand: true);
        if (created != null)
            HandMaterialTemplates[packVariantIndex] = created;

        return created;
    }

    static Material BuildPackMaterial(int packVariantIndex, bool forHand)
    {
        Material source = BuildPackMaterialCore(packVariantIndex);
        if (source == null)
            return null;

        Material material = BuildPackLitMaterial(source, forHand);
        if (Application.isPlaying)
            Object.Destroy(source);
        return material;
    }

    static Material BuildWorldMaterial(int packVariantIndex)
    {
        return BuildPackMaterial(packVariantIndex, forHand: false);
    }

    static Material BuildPackMaterialCore(int packVariantIndex)
    {
        string folder = GetVariantFolderResourcePath(packVariantIndex);
        string prefix = GetVariantFilePrefix(packVariantIndex);

        Material loaded = Resources.Load<Material>(folder + "/" + prefix + "World");
        if (loaded != null)
        {
            Material instance = Object.Instantiate(loaded);
            instance.name = loaded.name;
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

        return material;
    }

    static Material LoadPackToonTemplate()
    {
        if (_packToonTemplate != null)
            return _packToonTemplate;

        _packToonTemplate = Resources.Load<Material>(PackToonTemplateResourcePath);
#if UNITY_EDITOR
        if (_packToonTemplate == null)
            _packToonTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(PackToonTemplateAssetPath);
#endif
        return _packToonTemplate;
    }

    static Material ConvertToPackToonMaterial(Material source)
    {
        if (source == null)
            return null;

        Material template = LoadPackToonTemplate();
        if (template == null)
            return source;

        if (source.shader == template.shader)
        {
            ApplyPackToonFeatures(source, source);
            return source;
        }

        Material toonMaterial = ExteriorWallToonUtility.CreateToonMaterial(source, template);
        toonMaterial.name = source.name;
        ApplyPackToonFeatures(toonMaterial, source);
        return toonMaterial;
    }

    static void ApplyPackToonFeatures(Material toonMaterial, Material source)
    {
        CopySourceTexture(source, toonMaterial, "_SpecGlossMap");

        toonMaterial.SetColor("_BaseColor", Color.white);
        toonMaterial.SetColor("_Color", Color.white);
        toonMaterial.SetColor("_HColor", Color.white);
        toonMaterial.SetColor("_SColor", Color.white);

        if (toonMaterial.HasProperty("_RampThreshold"))
            toonMaterial.SetFloat("_RampThreshold", 0.697f);
        if (toonMaterial.HasProperty("_RampSmoothing"))
            toonMaterial.SetFloat("_RampSmoothing", 0f);

        if (toonMaterial.HasProperty("_IndirectIntensity"))
            toonMaterial.SetFloat("_IndirectIntensity", 0.205f);
        if (toonMaterial.HasProperty("_SingleIndirectColor"))
            toonMaterial.SetFloat("_SingleIndirectColor", 1f);

        if (toonMaterial.HasProperty("_ShadowColorLightAtten"))
            toonMaterial.SetFloat("_ShadowColorLightAtten", 0f);
        toonMaterial.DisableKeyword("TCP2_SHADOW_LIGHT_COLOR");

        if (toonMaterial.GetTexture("_BumpMap") != null && toonMaterial.HasProperty("_UseNormalMap"))
            toonMaterial.SetFloat("_UseNormalMap", 1f);

        if (toonMaterial.HasProperty("_UseSpecular"))
            toonMaterial.SetFloat("_UseSpecular", 1f);
        toonMaterial.EnableKeyword("TCP2_SPECULAR");
        toonMaterial.DisableKeyword("TCP2_SPECULAR_STYLIZED");
        toonMaterial.DisableKeyword("TCP2_SPECULAR_CRISP");

        if (toonMaterial.HasProperty("_SpecularType"))
            toonMaterial.SetFloat("_SpecularType", 0f);

        if (toonMaterial.HasProperty("_SpecularColor"))
            toonMaterial.SetColor("_SpecularColor", new Color(0.451f, 0.451f, 0.451f, 1f));

        if (toonMaterial.HasProperty("_SpecularRoughness"))
            toonMaterial.SetFloat("_SpecularRoughness", 0.426f);

        if (toonMaterial.HasProperty("_UseReflections"))
            toonMaterial.SetFloat("_UseReflections", 0f);
        toonMaterial.DisableKeyword("TCP2_REFLECTIONS");

        if (toonMaterial.HasProperty("_UseEmission"))
            toonMaterial.SetFloat("_UseEmission", 0f);
        if (toonMaterial.HasProperty("_UseRim"))
            toonMaterial.SetFloat("_UseRim", 0f);
        if (toonMaterial.HasProperty("_UseMatCap"))
            toonMaterial.SetFloat("_UseMatCap", 0f);
        if (toonMaterial.HasProperty("_UseOcclusion"))
            toonMaterial.SetFloat("_UseOcclusion", 0f);
        if (toonMaterial.HasProperty("_UseOutline"))
            toonMaterial.SetFloat("_UseOutline", 0f);

        ApplyPackNoShadowMaterialSettings(toonMaterial);
    }

    static void CopySourceTexture(Material source, Material destination, string propertyName)
    {
        if (source == null || destination == null)
            return;

        if (!source.HasProperty(propertyName) || !destination.HasProperty(propertyName))
            return;

        Texture texture = source.GetTexture(propertyName);
        if (texture == null)
            return;

        destination.SetTexture(propertyName, texture);
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
    /// Flat URP Lit for packs — same look in hand and on the ground.
    /// </summary>
    static Material BuildPackLitMaterial(Material source, bool forHand)
    {
        if (source == null)
            return null;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        var material = new Material(shader)
        {
            name = source.name + (forHand ? "_Hand" : "_World")
        };

        CopyAlbedoTexture(source, material);
        SharpenPackAlbedo(material);
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_Color", Color.white);

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0f);

        ApplyPackTextureUFlip(material);
        if (forHand)
            ConfigurePackHandMaterial(material);
        else
            ConfigurePackWorldMaterial(material);

        return material;
    }

    static void CopyAlbedoTexture(Material source, Material destination)
    {
        if (source == null || destination == null)
            return;

        if (source.HasProperty("_BaseMap") && destination.HasProperty("_BaseMap"))
            destination.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
        else if (source.HasProperty("_MainTex") && destination.HasProperty("_BaseMap"))
            destination.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
        else if (source.HasProperty("_MainTex") && destination.HasProperty("_MainTex"))
            destination.SetTexture("_MainTex", source.GetTexture("_MainTex"));
    }

    static void SharpenPackAlbedo(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseMap"))
            CardArtLibrary.SharpenGroundItemTexture(material.GetTexture("_BaseMap"));
        if (material.HasProperty("_MainTex"))
            CardArtLibrary.SharpenGroundItemTexture(material.GetTexture("_MainTex"));
    }

    public static void ConfigurePackHandMaterial(Material material)
    {
        if (material == null)
            return;

        ApplyPackNoShadowMaterialSettings(material);

        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Back);

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 1f);

        // Default opaque queue — 2501 caused see-through hand cards/packs.
        material.renderQueue = (int)RenderQueue.Geometry;
    }

    static void ApplyPackNoShadowMaterialSettings(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_ReceiveShadows"))
            material.SetFloat("_ReceiveShadows", 0f);
        if (material.HasProperty("_ReceiveShadowsOff"))
            material.SetFloat("_ReceiveShadowsOff", 1f);
        material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.enableInstancing = false;

        if (material.HasProperty("_ShadowColorLightAtten"))
            material.SetFloat("_ShadowColorLightAtten", 0f);
        material.DisableKeyword("TCP2_SHADOW_LIGHT_COLOR");
    }

    static void ApplyPackHandFlatPresentation(Material material)
    {
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_Color", Color.white);
        material.SetColor("_HColor", Color.white);
        material.SetColor("_SColor", Color.white);

        if (material.HasProperty("_RampThreshold"))
            material.SetFloat("_RampThreshold", 0f);
        if (material.HasProperty("_RampSmoothing"))
            material.SetFloat("_RampSmoothing", 1f);
        if (material.HasProperty("_RampOffset"))
            material.SetFloat("_RampOffset", 0f);

        if (material.HasProperty("_IndirectIntensity"))
            material.SetFloat("_IndirectIntensity", 1f);
        if (material.HasProperty("_SingleIndirectColor"))
            material.SetFloat("_SingleIndirectColor", 1f);

        if (material.HasProperty("_UseNormalMap"))
            material.SetFloat("_UseNormalMap", 0f);

        if (material.HasProperty("_UseSpecular"))
            material.SetFloat("_UseSpecular", 0f);
        material.DisableKeyword("TCP2_SPECULAR");
        material.DisableKeyword("TCP2_SPECULAR_STYLIZED");
        material.DisableKeyword("TCP2_SPECULAR_CRISP");

        if (material.HasProperty("_UseRim"))
            material.SetFloat("_UseRim", 0f);
        material.DisableKeyword("TCP2_RIM_LIGHTING");

        if (material.HasProperty("_UseReflections"))
            material.SetFloat("_UseReflections", 0f);
        material.DisableKeyword("TCP2_REFLECTIONS");

        if (material.HasProperty("_UseEmission"))
            material.SetFloat("_UseEmission", 0f);
    }

    /// <summary>
    /// Ground packs: same lit look as hand; double-sided for mirrored ground pose.
    /// </summary>
    public static void ConfigurePackWorldMaterial(Material material)
    {
        if (material == null)
            return;

        ApplyPackNoShadowMaterialSettings(material);

        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 1f);

        material.renderQueue = 2501;
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

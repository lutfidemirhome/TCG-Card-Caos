using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Loads booster pack PBR textures from Resources and applies them to imported pack meshes.
/// </summary>
public static class PackArtLibrary
{
    const string WorldMaterialResourcePath = "Cards/BoosterPack/BoosterPackWorld";
    const string BaseColorResourcePath = "Cards/BoosterPack/cards_DefaultMaterial_BaseColor";
    const string NormalResourcePath = "Cards/BoosterPack/cards_DefaultMaterial_Normal";
    const string MetallicResourcePath = "Cards/BoosterPack/cards_DefaultMaterial_Metallic";
    const string RoughnessResourcePath = "Cards/BoosterPack/cards_DefaultMaterial_Roughness";

    static Material _worldMaterialTemplate;

    public static void ApplyPackMaterials(Renderer renderer)
    {
        if (renderer == null)
            return;

        Material template = GetWorldMaterialTemplate();
        if (template == null)
            return;

        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
            materials[i] = template;

        renderer.sharedMaterials = materials;
    }

    public static void ApplyPackMaterials(Transform visualRoot)
    {
        if (visualRoot == null)
            return;

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            ApplyPackMaterials(renderers[i]);
    }

    static Material GetWorldMaterialTemplate()
    {
        if (_worldMaterialTemplate != null)
            return _worldMaterialTemplate;

        Material loaded = Resources.Load<Material>(WorldMaterialResourcePath);
        if (loaded != null)
        {
            _worldMaterialTemplate = Object.Instantiate(loaded);
            _worldMaterialTemplate.name = loaded.name;
            ConfigurePackWorldMaterial(_worldMaterialTemplate);
            return _worldMaterialTemplate;
        }

        Texture2D baseColor = Resources.Load<Texture2D>(BaseColorResourcePath);
        Texture2D normalMap = Resources.Load<Texture2D>(NormalResourcePath);
        Texture2D metallicMap = Resources.Load<Texture2D>(MetallicResourcePath);
        Texture2D roughnessMap = Resources.Load<Texture2D>(RoughnessResourcePath);

        if (baseColor == null)
        {
            Debug.LogWarning("PackArtLibrary: Base color texture missing at " + BaseColorResourcePath);
            return null;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        _worldMaterialTemplate = new Material(shader) { name = "BoosterPackWorld" };
        _worldMaterialTemplate.SetTexture("_BaseMap", baseColor);
        _worldMaterialTemplate.SetTexture("_MainTex", baseColor);

        if (normalMap != null)
        {
            _worldMaterialTemplate.SetTexture("_BumpMap", normalMap);
            _worldMaterialTemplate.EnableKeyword("_NORMALMAP");
        }

        if (metallicMap != null)
        {
            _worldMaterialTemplate.SetTexture("_MetallicGlossMap", metallicMap);
            _worldMaterialTemplate.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (roughnessMap != null && _worldMaterialTemplate.HasProperty("_SpecGlossMap"))
            _worldMaterialTemplate.SetTexture("_SpecGlossMap", roughnessMap);

        if (_worldMaterialTemplate.HasProperty("_Metallic"))
            _worldMaterialTemplate.SetFloat("_Metallic", metallicMap != null ? 1f : 0.15f);

        if (_worldMaterialTemplate.HasProperty("_Smoothness"))
            _worldMaterialTemplate.SetFloat("_Smoothness", roughnessMap != null ? 1f : 0.45f);

        ConfigurePackWorldMaterial(_worldMaterialTemplate);
        return _worldMaterialTemplate;
    }

    /// <summary>
    /// Single-image pack uses the same art on both faces; render both sides so a horizontal
    /// visual mirror (WorldVisualScale) never swaps which physical face you see.
    /// </summary>
    public static void ConfigurePackWorldMaterial(Material material)
    {
        if (material == null)
            return;

        CardArtLibrary.ConfigureGroundWorldMaterial(material);

        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);
    }
}

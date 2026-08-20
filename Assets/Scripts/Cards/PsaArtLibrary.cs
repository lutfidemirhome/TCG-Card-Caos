using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// PSA slab textures and materials. Textures live under Resources/Cards/PsaCard/psa_N/card_diffuseMAT
/// </summary>
public static class PsaArtLibrary
{
    public const int VariantCount = 4;
    const string ModelResourcePath = "Cards/PsaCard/plastic_card_holder";
    const string TextureFileName = "card_diffuseMAT";

    static GameObject _modelPrefab;
    static Material _sharedSlabTemplate;

    public static GameObject LoadModelPrefab()
    {
        if (_modelPrefab == null)
            _modelPrefab = Resources.Load<GameObject>(ModelResourcePath);

        return _modelPrefab;
    }

    public static Texture2D GetVariantTexture(int variantIndex)
    {
        variantIndex = Mathf.Clamp(variantIndex, 1, VariantCount);
        string path = $"Cards/PsaCard/psa_{variantIndex}/{TextureFileName}";
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null && variantIndex != 1)
            texture = Resources.Load<Texture2D>($"Cards/PsaCard/psa_1/{TextureFileName}");
        return texture;
    }

    public static Material CreateSlabMaterial(Texture2D texture)
    {
        Material template = GetSharedSlabTemplate();
        var material = new Material(template);
        ApplyCardTexture(material, texture);
        CardArtLibrary.ConfigureHandDetailMaterial(material);
        return material;
    }

    public static void ApplySlabMaterials(Transform modelRoot, int variantIndex)
    {
        if (modelRoot == null)
            return;

        Texture2D texture = GetVariantTexture(variantIndex);

        Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || IsOutlineObject(renderer.gameObject))
                continue;

            Material[] source = renderer.sharedMaterials;
            if (source == null || source.Length == 0)
                continue;

            var materials = new Material[source.Length];
            for (int m = 0; m < source.Length; m++)
            {
                Material sourceMaterial = source[m];
                if (sourceMaterial == null)
                    continue;

                if (IsCardDiffuseMaterial(sourceMaterial))
                {
                    materials[m] = new Material(sourceMaterial);
                    ApplyCardTexture(materials[m], texture);
                    CardArtLibrary.ConfigureHandDetailMaterial(materials[m]);
                }
                else
                {
                    materials[m] = sourceMaterial;
                }
            }

            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    static void ApplyCardTexture(Material material, Texture2D texture)
    {
        if (material == null || texture == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
    }

    static bool IsCardDiffuseMaterial(Material material)
    {
        string materialName = material.name;
        return materialName.IndexOf("Card", StringComparison.OrdinalIgnoreCase) >= 0
            || materialName.IndexOf("Diffuse", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static Material GetSharedSlabTemplate()
    {
        if (_sharedSlabTemplate != null)
            return _sharedSlabTemplate;

        CardArtLibrary.EnsureLoaded();
        _sharedSlabTemplate = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        _sharedSlabTemplate.name = "PsaSlabTemplate";
        return _sharedSlabTemplate;
    }

    static bool IsOutlineObject(GameObject gameObject)
    {
        if (gameObject == null)
            return false;

        string objectName = gameObject.name;
        return objectName == "InteractionOutline" || objectName == "HandSelectionOutline";
    }
}

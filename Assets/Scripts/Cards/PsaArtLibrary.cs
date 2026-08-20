using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// PSA slab art under Resources/Cards/PsaCard/.
/// Cabinet slots 7–10 (left→right). Each slot folder holds variant subfolders:
/// psa_7/psa_7_1/card_diffuseMAT.png, psa_7_1_Preview.png, psa_7_2/…
/// </summary>
public static class PsaArtLibrary
{
    public static readonly int[] CabinetSlotNumbers = { 7, 8, 9, 10 };
    public const int CabinetSlotCount = 4;
    public const int MinCabinetSlotNumber = 7;
    public const int MaxCabinetSlotNumber = 10;
    public const int MaxVariantsPerSlot = 64;
    public const string SlabTextureFileName = "card_diffuseMAT";
    public const string PreviewTextureSuffix = "_Preview";
    const string ModelResourcePath = "Cards/PsaCard/plastic_card_holder";

    static GameObject _modelPrefab;
    static Material _sharedSlabTemplate;

    public static bool IsCabinetSlotNumber(int slotNumber) =>
        slotNumber >= MinCabinetSlotNumber && slotNumber <= MaxCabinetSlotNumber;

    public static int ClampCabinetSlotNumber(int slotNumber)
    {
        if (!IsCabinetSlotNumber(slotNumber))
            return MinCabinetSlotNumber;
        return slotNumber;
    }

    public static int SlotNumberToCabinetIndex(int slotNumber) => slotNumber - MinCabinetSlotNumber;

    public static string GetSlotFolderName(int slotNumber) => $"psa_{ClampCabinetSlotNumber(slotNumber)}";

    public static string GetVariantFolderName(int slotNumber, int variantIndex) =>
        $"{GetSlotFolderName(slotNumber)}_{Mathf.Max(1, variantIndex)}";

    public static string GetVariantResourceFolder(int slotNumber, int variantIndex) =>
        $"Cards/PsaCard/{GetSlotFolderName(slotNumber)}/{GetVariantFolderName(slotNumber, variantIndex)}";

    public static GameObject LoadModelPrefab()
    {
        if (_modelPrefab == null)
            _modelPrefab = Resources.Load<GameObject>(ModelResourcePath);

        return _modelPrefab;
    }

    public static Texture2D GetSlabTexture(int slotNumber, int variantIndex)
    {
        slotNumber = ClampCabinetSlotNumber(slotNumber);
        variantIndex = Mathf.Max(1, variantIndex);

        string path = $"{GetVariantResourceFolder(slotNumber, variantIndex)}/{SlabTextureFileName}";
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null && variantIndex != 1)
            texture = Resources.Load<Texture2D>($"{GetVariantResourceFolder(slotNumber, 1)}/{SlabTextureFileName}");
        return texture;
    }

    /// <summary>UI inspect preview — upright PNG per variant, like Pack0N_Preview.</summary>
    public static Texture2D GetVariantPreview(int slotNumber, int variantIndex)
    {
        slotNumber = ClampCabinetSlotNumber(slotNumber);
        variantIndex = Mathf.Max(1, variantIndex);

        string folder = GetVariantResourceFolder(slotNumber, variantIndex);
        string previewName = GetVariantFolderName(slotNumber, variantIndex) + PreviewTextureSuffix;
        Texture2D preview = Resources.Load<Texture2D>($"{folder}/{previewName}");
        if (preview == null && variantIndex != 1)
            preview = GetVariantPreview(slotNumber, 1);
        return preview;
    }

    public static bool HasVariant(int slotNumber, int variantIndex)
    {
        return GetSlabTexture(slotNumber, variantIndex) != null;
    }

    public static int CountVariantsInSlot(int slotNumber)
    {
        slotNumber = ClampCabinetSlotNumber(slotNumber);
        int count = 0;
        for (int variantIndex = 1; variantIndex <= MaxVariantsPerSlot; variantIndex++)
        {
            if (!HasVariant(slotNumber, variantIndex))
                break;

            count = variantIndex;
        }

        return Mathf.Max(1, count);
    }

    public static Material CreateSlabMaterial(int slotNumber, int variantIndex)
    {
        Material template = GetSharedSlabTemplate();
        var material = new Material(template);
        ApplyCardTexture(material, GetSlabTexture(slotNumber, variantIndex));
        CardArtLibrary.ConfigureHandDetailMaterial(material);
        return material;
    }

    public static void ApplySlabMaterials(Transform modelRoot, int slotNumber, int variantIndex)
    {
        if (modelRoot == null)
            return;

        Texture2D texture = GetSlabTexture(slotNumber, variantIndex);

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

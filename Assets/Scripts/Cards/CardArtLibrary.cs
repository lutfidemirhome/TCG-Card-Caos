using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads the shared trading-card mesh and front/back materials.
/// Run "TCG Card Caos → Setup Card Art" once in the editor to bake runtime assets.
/// </summary>
public static class CardArtLibrary
{
    public const string FrontTextureAssetPath = "Assets/Art/Cards/card_texture_ön_test.png";
    public const string BackTextureAssetPath = "Assets/Art/Cards/kart_arka_template.png";
    public const string FrontMaterialAssetPath = "Assets/Art/Cards/CardFront.mat";
    public const string BackMaterialAssetPath = "Assets/Art/Cards/CardBack.mat";

    public const string RuntimeMeshResourcePath = "Cards/TradingCardMesh";
    public const string RuntimeInstancedMeshResourcePath = "Cards/InstancedCardMesh";
    public const string RuntimeInstancedBackMeshResourcePath = "Cards/InstancedCardBackMesh";
    public const string RuntimeFrontWorldMaterialResourcePath = "Cards/CardFrontWorld";
    public const string RuntimeBackWorldMaterialResourcePath = "Cards/CardBackWorld";
    public const string RuntimeFrontDetailMaterialResourcePath = "Cards/CardFrontDetail";
    public const string RuntimeBackDetailMaterialResourcePath = "Cards/CardBackDetail";

    /// <summary>Lays the imported upright card model flat on the table.</summary>
    public static readonly Quaternion WorldVisualRotation = Quaternion.Euler(-90f, 0f, 0f);

    /// <summary>Cancels horizontal mirror for flat ground cards (left-right text).</summary>
    public static readonly Vector3 WorldVisualScale = new Vector3(-1f, 1f, 1f);

    /// <summary>Orientates the textured face toward the camera in the hand fan.</summary>
    public static readonly Quaternion HandVisualRotation = Quaternion.Euler(-90f, 180f, 0f);

    /// <summary>Cancels the horizontal mirror from HandVisualRotation (left-right symmetry).</summary>
    public static readonly Vector3 HandVisualScale = new Vector3(-1f, 1f, 1f);

    /// <summary>Upright card on shelf slots (+Y height, +Z face).</summary>
    public static readonly Quaternion ShelfVisualRotation = Quaternion.identity;

    /// <summary>Cancels horizontal mirror for upright shelf cards (left-right text).</summary>
    public static readonly Vector3 ShelfVisualScale = new Vector3(-1f, 1f, 1f);

    /// <summary>Shader UV scale/offset for shared back materials (cancels mesh back flipU + visual scale X = -1).</summary>
    public static readonly Vector2 BackTextureUScale = new Vector2(-1f, 1f);

    /// <summary>Shader UV offset paired with <see cref="BackTextureUScale"/>.</summary>
    public static readonly Vector2 BackTextureUOffset = new Vector2(1f, 0f);

    public static readonly Quaternion ModelCorrectionRotation = WorldVisualRotation;

    static Mesh _cardMesh;
    static Mesh _instancedCardMesh;
    static Mesh _instancedCardBackMesh;
    static Material _sharedFrontWorldTemplate;
    static Material _sharedBackWorldTemplate;
    static Material _sharedFrontDetailTemplate;
    static Material _sharedBackDetailTemplate;
    static Vector3? _flatSize;
    static Rect? _frontArtUvRect;
    static readonly Dictionary<int, Material> FrontWorldMaterialsByPalette = new Dictionary<int, Material>();
    static readonly Dictionary<int, Material> FrontDetailMaterialsByPalette = new Dictionary<int, Material>();
    static readonly Dictionary<string, Material> FrontWorldMaterialsByDefinition = new Dictionary<string, Material>();
    static readonly Dictionary<string, Material> FrontDetailMaterialsByDefinition = new Dictionary<string, Material>();

    public static float FlatWidth => FlatSize.x;
    public static float FlatHeight => FlatSize.z;
    public static float FlatThickness => FlatSize.y;

    public static Vector3 FlatSize
    {
        get
        {
            EnsureLoaded();
            if (_flatSize == null)
                _flatSize = ComputeFlatSize(_cardMesh);

            return _flatSize.Value;
        }
    }

    public static Bounds MeshBounds
    {
        get
        {
            EnsureLoaded();
            return _cardMesh != null ? _cardMesh.bounds : new Bounds(Vector3.zero, new Vector3(0.126f, 0.176f, 0.004f));
        }
    }

    /// <summary>Corner radius of the card mesh silhouette in mesh-local XY space.</summary>
    public static float MeshCornerRadius => CardModelDimensions.CornerRadius;

    public static Mesh CardMesh
    {
        get
        {
            EnsureLoaded();
            return _cardMesh;
        }
    }

    /// <summary>Lightweight front-face quad for GPU-instanced ground cards.</summary>
    public static Mesh InstancedCardMesh
    {
        get
        {
            EnsureLoaded();
            return _instancedCardMesh != null ? _instancedCardMesh : _cardMesh;
        }
    }

    /// <summary>Lightweight back-face quad for GPU-instanced face-down ground cards.</summary>
    public static Mesh InstancedCardBackMesh
    {
        get
        {
            EnsureLoaded();
            return _instancedCardBackMesh != null ? _instancedCardBackMesh : _instancedCardMesh;
        }
    }

    /// <summary>UV rect of the readable front art, for screen-space inspect UI.</summary>
    public static Rect FrontArtUvRect
    {
        get
        {
            EnsureLoaded();
            if (_frontArtUvRect == null)
            {
                if (_cardMesh != null && CardMeshBuilder.TryGetFrontFaceUvRect(_cardMesh, out Rect rect))
                    _frontArtUvRect = rect;
                else
                    _frontArtUvRect = new Rect(0f, 0f, 1f, 1f);
            }

            return _frontArtUvRect.Value;
        }
    }

    public static Material GetBackMaterial(CardTextureQuality quality)
    {
        EnsureLoaded();
        Material material = quality == CardTextureQuality.World
            ? _sharedBackWorldTemplate
            : _sharedBackDetailTemplate;
        ApplyBackTextureUFlip(material);
        return material;
    }

    public static Material GetFrontMaterial(int paletteIndex, CardTextureQuality quality = CardTextureQuality.Detail)
    {
        EnsureLoaded();

        Dictionary<int, Material> cache = quality == CardTextureQuality.World
            ? FrontWorldMaterialsByPalette
            : FrontDetailMaterialsByPalette;

        Material template = quality == CardTextureQuality.World
            ? _sharedFrontWorldTemplate
            : _sharedFrontDetailTemplate;

        if (!cache.TryGetValue(paletteIndex, out Material frontMaterial))
        {
            frontMaterial = new Material(template);
            if (quality == CardTextureQuality.World)
                frontMaterial.enableInstancing = true;

            cache[paletteIndex] = frontMaterial;
        }

        return frontMaterial;
    }

    public static Material GetFrontMaterial(CardDefinition definition, CardTextureQuality quality = CardTextureQuality.Detail)
    {
        EnsureLoaded();

        if (definition == null || definition.FrontTexture == null)
            return GetFrontMaterial(0, quality);

        Dictionary<string, Material> cache = quality == CardTextureQuality.World
            ? FrontWorldMaterialsByDefinition
            : FrontDetailMaterialsByDefinition;

        Material template = quality == CardTextureQuality.World
            ? _sharedFrontWorldTemplate
            : _sharedFrontDetailTemplate;

        string cacheKey = definition.DefinitionId + ":" + (int)quality;
        if (!cache.TryGetValue(cacheKey, out Material frontMaterial))
        {
            frontMaterial = new Material(template);
            ApplyFrontTexture(frontMaterial, definition.FrontTexture);
            if (quality == CardTextureQuality.World)
                frontMaterial.enableInstancing = true;

            cache[cacheKey] = frontMaterial;
        }

        return frontMaterial;
    }

    static void ApplyFrontTexture(Material material, Texture2D texture)
    {
        if (material == null || texture == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
    }

    public static Material[] GetCardMaterials(int paletteIndex, CardTextureQuality quality = CardTextureQuality.Detail)
    {
        return new[]
        {
            GetFrontMaterial(paletteIndex, quality),
            GetBackMaterial(quality),
        };
    }

    public static Material[] GetCardMaterials(CardDefinition definition, CardTextureQuality quality = CardTextureQuality.Detail)
    {
        EnsureLoaded();
        return new[]
        {
            GetFrontMaterial(definition, quality),
            GetBackMaterial(quality),
        };
    }

    public static Texture2D GetDefinitionFrontTexture(CardDefinition definition)
    {
        return definition != null ? definition.FrontTexture : null;
    }

    public static void ResetCache()
    {
        _cardMesh = null;
        _instancedCardMesh = null;
        _instancedCardBackMesh = null;
        _sharedFrontWorldTemplate = null;
        _sharedBackWorldTemplate = null;
        _sharedFrontDetailTemplate = null;
        _sharedBackDetailTemplate = null;
        _flatSize = null;
        _frontArtUvRect = null;
        FrontWorldMaterialsByPalette.Clear();
        FrontDetailMaterialsByPalette.Clear();
        FrontWorldMaterialsByDefinition.Clear();
        FrontDetailMaterialsByDefinition.Clear();
        CardVisualResources.ResetOutlineCache();
    }

    public static void EnsureLoaded()
    {
        if (_cardMesh != null
            && _sharedFrontWorldTemplate != null
            && _sharedBackWorldTemplate != null
            && _sharedFrontDetailTemplate != null
            && _sharedBackDetailTemplate != null)
        {
            return;
        }

        if (!TryLoadRuntimeAssets())
        {
            Debug.LogError(
                "CardArtLibrary: Missing runtime card assets. In Unity, run TCG Card Caos → Setup Card Art, then respawn cards.");
        }
    }

    static bool TryLoadRuntimeAssets()
    {
        _cardMesh = Resources.Load<Mesh>(RuntimeMeshResourcePath);
        _instancedCardMesh = Resources.Load<Mesh>(RuntimeInstancedMeshResourcePath);
        _instancedCardBackMesh = Resources.Load<Mesh>(RuntimeInstancedBackMeshResourcePath);
        if (_instancedCardBackMesh == null)
            _instancedCardBackMesh = CardMeshBuilder.CreatePrototypeInstancedBackQuad();
        _sharedFrontWorldTemplate = Resources.Load<Material>(RuntimeFrontWorldMaterialResourcePath);
        _sharedBackWorldTemplate = Resources.Load<Material>(RuntimeBackWorldMaterialResourcePath);
        _sharedFrontDetailTemplate = Resources.Load<Material>(RuntimeFrontDetailMaterialResourcePath);
        _sharedBackDetailTemplate = Resources.Load<Material>(RuntimeBackDetailMaterialResourcePath);

        if (_sharedFrontDetailTemplate == null)
            _sharedFrontDetailTemplate = Resources.Load<Material>("Cards/CardFrontDetail");

        if (_sharedBackDetailTemplate == null)
            _sharedBackDetailTemplate = Resources.Load<Material>("Cards/CardBackDetail");

        if (_sharedFrontWorldTemplate == null)
            _sharedFrontWorldTemplate = _sharedFrontDetailTemplate;

        if (_sharedBackWorldTemplate == null)
            _sharedBackWorldTemplate = _sharedBackDetailTemplate;

        ApplyBackTextureUFlip(_sharedBackWorldTemplate);
        ApplyBackTextureUFlip(_sharedBackDetailTemplate);

        return _cardMesh != null && _sharedFrontDetailTemplate != null && _sharedBackDetailTemplate != null;
    }

    /// <summary>
    /// Back submesh UVs are pre-flipped; combined with World/Shelf/Hand scale X = -1 the art reads mirrored.
    /// Shader U flip cancels the mesh flip so the back matches the front left-right orientation.
    /// </summary>
    public static void ApplyBackTextureUFlip(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTextureScale("_BaseMap", BackTextureUScale);
            material.SetTextureOffset("_BaseMap", BackTextureUOffset);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTextureScale("_MainTex", BackTextureUScale);
            material.SetTextureOffset("_MainTex", BackTextureUOffset);
        }
    }

    static Vector3 ComputeFlatSize(Mesh mesh)
    {
        if (mesh == null)
            return new Vector3(CardModelDimensions.Width, CardModelDimensions.Thickness, CardModelDimensions.Height);

        Vector3 meshSize = mesh.bounds.size;
        return new Vector3(meshSize.x, meshSize.z, meshSize.y);
    }
}

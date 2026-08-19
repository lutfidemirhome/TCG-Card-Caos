using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Loads the shared trading-card mesh and front/back materials.
/// Run "TCG Card Caos → Refresh Card Textures From Templates" after editing template PNGs
/// (or reimport the PNG — copies under Assets/Resources/Cards update automatically).
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

    /// <summary>Pitches a reveal card flat toward the camera (same basis as the hand fan).</summary>
    public static readonly Quaternion RevealRootLocalRotation =
        Quaternion.FromToRotation(Vector3.up, -Vector3.forward);

    /// <summary>Pack reveal: front submesh (+Z) toward the camera (matches the hand fan).</summary>
    public static readonly Quaternion RevealFrontVisualLocalRotation = HandVisualRotation;

    /// <summary>
    /// Pack reveal: back toward camera, logo upright. X shows the back face; Z corrects art orientation.
    /// </summary>
    public static readonly Quaternion RevealBackVisualLocalRotation =
        HandVisualRotation * Quaternion.Euler(180f, 0f, 180f);

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
    static Mesh _handCardMesh;
    static Mesh _instancedCardMesh;
    static Mesh _instancedCardBackMesh;
    static Material _sharedFrontWorldTemplate;
    static Material _sharedBackWorldTemplate;
    static Material _sharedFrontDetailTemplate;
    static Material _sharedBackDetailTemplate;
    static Material _instancedGroundBackMaterial;
    static Material _runtimeMeshBackDetailMaterial;
    static Material _runtimeMeshBackWorldMaterial;
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

    /// <summary>
    /// Front/back faces only — no perimeter edge strips. Hand cards face the camera directly,
    /// so edge UVs would show the art's black padding as a jagged top border.
    /// </summary>
    public static Mesh HandCardMesh
    {
        get
        {
            EnsureLoaded();
            if (_handCardMesh == null && _cardMesh != null)
            {
                _handCardMesh = CardMeshBuilder.CreateTradingCardMeshFromReference(
                    _cardMesh,
                    includeEdgeGeometry: false);
                if (_handCardMesh != null)
                    _handCardMesh.name = "HandCardMesh";
            }

            return _handCardMesh != null ? _handCardMesh : _cardMesh;
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

        if (quality == CardTextureQuality.World)
        {
            if (_runtimeMeshBackWorldMaterial == null)
            {
                _runtimeMeshBackWorldMaterial = new Material(_sharedBackWorldTemplate)
                {
                    name = "CardBackWorldMeshRuntime"
                };
                ApplyBackTextureUFlip(_runtimeMeshBackWorldMaterial);
                ConfigureGroundWorldMaterial(_runtimeMeshBackWorldMaterial);
            }

            return _runtimeMeshBackWorldMaterial;
        }

        if (_runtimeMeshBackDetailMaterial == null)
        {
            _runtimeMeshBackDetailMaterial = new Material(_sharedBackDetailTemplate)
            {
                name = "CardBackDetailMeshRuntime"
            };
            ApplyBackTextureUFlip(_runtimeMeshBackDetailMaterial);
            ConfigureHandDetailMaterial(_runtimeMeshBackDetailMaterial);
        }

        return _runtimeMeshBackDetailMaterial;
    }

    /// <summary>
    /// Instanced face-down ground cards: mesh UVs are U-flipped like fronts, with no WorldVisualScale.x = -1.
    /// V flip cancels the instanced draw's 180° X rotation; U flip keeps the back aligned with in-flight art.
    /// Do not use <see cref="ApplyBackTextureUFlip"/> here — that flip is only for shelf/hand/detail back faces.
    /// </summary>
    public static Material GetInstancedGroundBackMaterial()
    {
        EnsureLoaded();
        if (_instancedGroundBackMaterial == null)
        {
            _instancedGroundBackMaterial = new Material(_sharedBackWorldTemplate)
            {
                name = "CardBackWorldInstancedGround",
                enableInstancing = true,
            };
            ApplyInstancedGroundBackTextureTransform(_instancedGroundBackMaterial);
            ConfigureGroundWorldMaterial(_instancedGroundBackMaterial);
        }

        return _instancedGroundBackMaterial;
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
            {
                frontMaterial.enableInstancing = true;
                ConfigureGroundWorldMaterial(frontMaterial);
            }
            else
            {
                ConfigureHandDetailMaterial(frontMaterial);
            }

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
            {
                frontMaterial.enableInstancing = true;
                ConfigureGroundWorldMaterial(frontMaterial);
            }
            else
            {
                ConfigureHandDetailMaterial(frontMaterial);
            }

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
        _handCardMesh = null;
        _instancedCardMesh = null;
        _instancedCardBackMesh = null;
        _instancedGroundBackMaterial = null;
        _runtimeMeshBackDetailMaterial = null;
        _runtimeMeshBackWorldMaterial = null;
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
        _instancedCardMesh = PrepareInstancedFrontMesh(_instancedCardMesh);
        _instancedCardBackMesh = Resources.Load<Mesh>(RuntimeInstancedBackMeshResourcePath);
        if (_instancedCardBackMesh == null)
            _instancedCardBackMesh = CardMeshBuilder.CreatePrototypeInstancedBackQuad();
        _instancedCardBackMesh = PrepareInstancedFrontMesh(_instancedCardBackMesh);
        _sharedFrontWorldTemplate = Resources.Load<Material>(RuntimeFrontWorldMaterialResourcePath);
        _sharedBackWorldTemplate = Resources.Load<Material>(RuntimeBackWorldMaterialResourcePath);
        _sharedFrontDetailTemplate = Resources.Load<Material>(RuntimeFrontDetailMaterialResourcePath);
        _sharedBackDetailTemplate = Resources.Load<Material>(RuntimeBackDetailMaterialResourcePath);

        if (_sharedFrontDetailTemplate == null)
            _sharedFrontDetailTemplate = Resources.Load<Material>("Cards/CardFrontDetail");

        if (_sharedBackDetailTemplate == null)
            _sharedBackDetailTemplate = Resources.Load<Material>("Cards/CardBackDetail");

        if (_sharedFrontWorldTemplate == null && _sharedFrontDetailTemplate != null)
        {
            _sharedFrontWorldTemplate = new Material(_sharedFrontDetailTemplate)
            {
                name = "CardFrontWorld_RuntimeFallback"
            };
        }

        if (_sharedBackWorldTemplate == null && _sharedBackDetailTemplate != null)
        {
            _sharedBackWorldTemplate = new Material(_sharedBackDetailTemplate)
            {
                name = "CardBackWorld_RuntimeFallback"
            };
        }

        return _cardMesh != null && _sharedFrontDetailTemplate != null && _sharedBackDetailTemplate != null;
    }

    /// <summary>
    /// GPU-instanced ground quads only. Queue 2501 draws after SSAO; mesh cards must stay opaque (Detail).
    /// </summary>
    public static void ConfigureGroundWorldMaterial(Material material)
    {
        if (material == null)
            return;

        ApplyNoShadowMaterialSettings(material);
        ForceOpaqueSurface(material);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 1f);

        material.renderQueue = 2501;
    }

    /// <summary>
    /// Mesh cards (hand, throw, shelf flight): URP Lit Transparent surface with alpha 1 — matches the
    /// Inspector fix where switching Opaque → Transparent stops the card reading see-through.
    /// </summary>
    public static void ConfigureHandDetailMaterial(Material material)
    {
        if (material == null)
            return;

        ApplyNoShadowMaterialSettings(material);
        ForceTransparentSurface(material);
    }

    static void ApplyNoShadowMaterialSettings(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_ReceiveShadows"))
            material.SetFloat("_ReceiveShadows", 0f);

        material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        material.SetShaderPassEnabled("ShadowCaster", false);
    }

    static void ForceOpaqueSurface(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 0f);

        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHABLEND_ON");

        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 1f);
    }

    static void ForceTransparentSurface(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHABLEND_ON");

        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_SrcBlendAlpha"))
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (material.HasProperty("_DstBlendAlpha"))
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);

        // Alpha stays 1 below, so these cards are visually solid and must keep writing depth.
        // With depth writes off the transparent queue resolves overlap by camera distance, which
        // disagrees with the hand fan order once the arc drops the outer cards — the card next to
        // the selected one then renders behind its neighbour and disappears completely.
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 1f);

        if (material.HasProperty("_BaseColor"))
        {
            Color baseColor = material.GetColor("_BaseColor");
            baseColor.a = 1f;
            material.SetColor("_BaseColor", baseColor);
        }

        if (material.HasProperty("_Color"))
        {
            Color color = material.GetColor("_Color");
            color.a = 1f;
            material.SetColor("_Color", color);
        }

        material.renderQueue = (int)RenderQueue.Transparent;
    }

    public static void ApplyGroundWorldRendererSettings(Renderer renderer)
    {
        if (renderer == null)
            return;

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Material[] materials = renderer.materials;
        for (int i = 0; i < materials.Length; i++)
            ConfigureGroundWorldMaterial(materials[i]);
        renderer.materials = materials;
    }

    /// <summary>
    /// Baked instanced quads ship with legacy UVs; flip U at runtime so ground fronts read correctly
    /// without negative instanced scale (which backface-culls the quad).
    /// </summary>
    static Mesh PrepareInstancedFrontMesh(Mesh source)
    {
        if (source == null)
            return CardMeshBuilder.CreatePrototypeInstancedQuad();

        Vector2[] uvs = source.uv;
        if (uvs == null || uvs.Length < 4)
            return source;

        // Already flipped: first corner maps to U=1 on the left vertex.
        if (Mathf.Approximately(uvs[0].x, 1f) && Mathf.Approximately(uvs[1].x, 0f))
            return source;

        var flipped = Object.Instantiate(source);
        flipped.name = source.name + "_FrontUFlipped";
        Vector2[] flippedUvs = flipped.uv;
        for (int i = 0; i < flippedUvs.Length; i++)
            flippedUvs[i] = new Vector2(1f - flippedUvs[i].x, flippedUvs[i].y);
        flipped.uv = flippedUvs;
        return flipped;
    }

    /// <summary>
    /// Detail/shelf/hand backs: mesh flipU + WorldVisualScale.x = -1 mirror the art horizontally.
    /// Shader U flip cancels that mirror. Instanced ground backs skip this — use
    /// <see cref="GetInstancedGroundBackMaterial"/> instead.
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

    static readonly Vector2 InstancedGroundBackTextureScale = new Vector2(-1f, -1f);
    static readonly Vector2 InstancedGroundBackTextureOffset = new Vector2(1f, 1f);

    static void ApplyInstancedGroundBackTextureTransform(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTextureScale("_BaseMap", InstancedGroundBackTextureScale);
            material.SetTextureOffset("_BaseMap", InstancedGroundBackTextureOffset);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTextureScale("_MainTex", InstancedGroundBackTextureScale);
            material.SetTextureOffset("_MainTex", InstancedGroundBackTextureOffset);
        }
    }

    static void ApplyIdentityTextureTransform(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);
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

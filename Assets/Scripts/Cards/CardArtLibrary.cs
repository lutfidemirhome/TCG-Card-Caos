using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads the shared trading-card mesh and front/back materials.
/// Run "TCG Card Caos → Setup Card Art" once in the editor to bake runtime assets.
/// </summary>
public static class CardArtLibrary
{
    public const string ModelAssetPath = "Assets/Art/Cards/yzma.fbx";
    public const string FrontMaterialAssetPath = "Assets/Art/Cards/CardFront.mat";
    public const string BackMaterialAssetPath = "Assets/Art/Cards/CardBack.mat";
    public const string RuntimeMeshResourcePath = "Cards/TradingCardMesh";
    public const string RuntimeFrontMaterialResourcePath = "Cards/CardFront";
    public const string RuntimeBackMaterialResourcePath = "Cards/CardBack";

    /// <summary>Lays the imported upright card model flat on the table.</summary>
    public static readonly Quaternion WorldVisualRotation = Quaternion.Euler(-90f, 0f, 0f);

    /// <summary>Orientates the textured face toward the camera in the hand fan.</summary>
    public static readonly Quaternion HandVisualRotation = Quaternion.Euler(-90f, 180f, 0f);

    public static readonly Quaternion ModelCorrectionRotation = WorldVisualRotation;

    static Mesh _cardMesh;
    static Material _sharedFrontTemplate;
    static Material _sharedBack;
    static Vector3? _flatSize;
    static float? _meshCornerRadius;
    static readonly Dictionary<int, Material> FrontMaterialsByPalette = new Dictionary<int, Material>();

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
            return _cardMesh != null ? _cardMesh.bounds : new Bounds(Vector3.zero, new Vector3(0.063f, 0.088f, 0.0008f));
        }
    }

    /// <summary>Corner radius of the card mesh silhouette in mesh-local XY space.</summary>
    public static float MeshCornerRadius
    {
        get
        {
            EnsureLoaded();
            if (_meshCornerRadius == null)
                _meshCornerRadius = EstimateMeshCornerRadius(_cardMesh, MeshBounds);

            return _meshCornerRadius.Value;
        }
    }

    public static Mesh CardMesh
    {
        get
        {
            EnsureLoaded();
            return _cardMesh;
        }
    }

    public static Material SharedBackMaterial
    {
        get
        {
            EnsureLoaded();
            return _sharedBack;
        }
    }

    public static Material GetFrontMaterial(int paletteIndex)
    {
        EnsureLoaded();

        if (!FrontMaterialsByPalette.TryGetValue(paletteIndex, out Material frontMaterial))
        {
            frontMaterial = new Material(_sharedFrontTemplate);
            frontMaterial.enableInstancing = true;
            FrontMaterialsByPalette[paletteIndex] = frontMaterial;
        }

        return frontMaterial;
    }

    public static Material[] GetCardMaterials(int paletteIndex)
    {
        return new[]
        {
            GetFrontMaterial(paletteIndex),
            SharedBackMaterial,
        };
    }

    public static void ResetCache()
    {
        _cardMesh = null;
        _sharedFrontTemplate = null;
        _sharedBack = null;
        _flatSize = null;
        _meshCornerRadius = null;
        FrontMaterialsByPalette.Clear();
        CardVisualResources.ResetOutlineCache();
    }

    public static void EnsureLoaded()
    {
        if (_cardMesh != null && _sharedFrontTemplate != null && _sharedBack != null)
            return;

        if (!TryLoadRuntimeAssets())
        {
            Debug.LogError(
                "CardArtLibrary: Missing runtime card assets. In Unity, run TCG Card Caos → Setup Card Art, then respawn cards.");
        }
    }

    static bool TryLoadRuntimeAssets()
    {
        _cardMesh = Resources.Load<Mesh>(RuntimeMeshResourcePath);
        _sharedFrontTemplate = Resources.Load<Material>(RuntimeFrontMaterialResourcePath);
        _sharedBack = Resources.Load<Material>(RuntimeBackMaterialResourcePath);
        return _cardMesh != null && _sharedFrontTemplate != null && _sharedBack != null;
    }

    static Vector3 ComputeFlatSize(Mesh mesh)
    {
        if (mesh == null)
            return new Vector3(0.063f, 0.0008f, 0.088f);

        Vector3 meshSize = mesh.bounds.size;
        // Imported mesh is upright: X=width, Y=length, Z=thickness. Flatten with -90° on X.
        return new Vector3(meshSize.x, meshSize.z, meshSize.y);
    }

    static float EstimateMeshCornerRadius(Mesh mesh, Bounds bounds)
    {
        if (mesh == null)
            return 0.004f;

        Vector3[] vertices = mesh.vertices;
        float halfW = bounds.extents.x;
        float halfH = bounds.extents.y;
        float frontZ = bounds.max.z;
        float zTolerance = bounds.extents.z * 0.6f;
        float edgeBand = Mathf.Max(halfH, halfW) * 0.015f;

        float EstimateFromCorner(float cornerX, float cornerY, bool useXEdge, bool usePositiveX, bool usePositiveY)
        {
            float estimate = Mathf.Min(halfW, halfH);

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                if (Mathf.Abs(v.z - frontZ) > zTolerance)
                    continue;

                if (usePositiveX ? v.x < 0f : v.x > 0f)
                    continue;

                if (usePositiveY ? v.y < 0f : v.y > 0f)
                    continue;

                if (useXEdge)
                {
                    if (Mathf.Abs(v.y - cornerY) > edgeBand)
                        continue;

                    estimate = Mathf.Min(estimate, Mathf.Abs(cornerX - v.x));
                }
                else
                {
                    if (Mathf.Abs(v.x - cornerX) > edgeBand)
                        continue;

                    estimate = Mathf.Min(estimate, Mathf.Abs(cornerY - v.y));
                }
            }

            return estimate;
        }

        float topRight = EstimateFromCorner(halfW, halfH, useXEdge: true, usePositiveX: true, usePositiveY: true);
        float topLeft = EstimateFromCorner(-halfW, halfH, useXEdge: true, usePositiveX: false, usePositiveY: true);
        float bottomRight = EstimateFromCorner(halfW, -halfH, useXEdge: true, usePositiveX: true, usePositiveY: false);
        float bottomLeft = EstimateFromCorner(-halfW, -halfH, useXEdge: true, usePositiveX: false, usePositiveY: false);

        float radius = Mathf.Min(topRight, topLeft, bottomRight, bottomLeft);
        if (radius > 0.0005f && radius < Mathf.Min(halfW, halfH) * 0.35f)
            return radius;

        return Mathf.Min(halfW, halfH) * 0.055f;
    }
}

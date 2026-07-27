using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Shared outline meshes/materials for card interaction and hand selection.
/// Built in mesh-local space and parented under CardVisual.
/// </summary>
static class CardVisualResources
{
    static readonly Color InteractionOutlineColor = new Color(1f, 0.88f, 0.12f);
    static readonly Color HandSelectionOutlineColor = Color.white;

    static Mesh _interactionBorderFrameMesh;
    static Mesh _handSelectionBorderFrameMesh;
    static Material _outlineMaterial;
    static Material _handSelectionOutlineMaterial;

    public static Material InteractionOutlineMaterial
    {
        get
        {
            EnsureInitialized();
            return _outlineMaterial;
        }
    }

    public static Mesh InteractionBorderFrameMesh
    {
        get
        {
            EnsureInitialized();
            return _interactionBorderFrameMesh;
        }
    }

    public static Material HandSelectionOutlineMaterial
    {
        get
        {
            EnsureInitialized();
            return _handSelectionOutlineMaterial;
        }
    }

    public static Mesh HandSelectionBorderFrameMesh
    {
        get
        {
            EnsureInitialized();
            return _handSelectionBorderFrameMesh;
        }
    }

    public static void ResetOutlineCache()
    {
        _interactionBorderFrameMesh = null;
        _handSelectionBorderFrameMesh = null;
    }

    static void EnsureInitialized()
    {
        CardArtLibrary.EnsureLoaded();

        if (_interactionBorderFrameMesh != null && _handSelectionBorderFrameMesh != null
            && _outlineMaterial != null && _handSelectionOutlineMaterial != null)
            return;

        _interactionBorderFrameMesh ??= BuildBorderFrameMesh(CardDimensions.InteractionOutlineThickness);
        _handSelectionBorderFrameMesh ??= BuildBorderFrameMesh(CardDimensions.HandSelectionOutlineThickness);
        _outlineMaterial ??= RuntimeMaterialUtility.CreateUnlitMaterial(
            InteractionOutlineColor,
            enableInstancing: true,
            renderQueue: (int)RenderQueue.Geometry + 1);
        _handSelectionOutlineMaterial ??= RuntimeMaterialUtility.CreateUnlitMaterial(
            HandSelectionOutlineColor,
            enableInstancing: true,
            renderQueue: (int)RenderQueue.Geometry + 2);
    }

    static Mesh BuildBorderFrameMesh(float borderThickness)
    {
        Bounds bounds = CardArtLibrary.MeshBounds;
        float halfWidth = bounds.extents.x;
        float halfHeight = bounds.extents.y;
        float halfThickness = bounds.extents.z;
        float z = halfThickness * 0.65f;
        float stripDepth = Mathf.Max(halfThickness * 0.35f * 2f, borderThickness);

        var combine = new CombineInstance[4];
        combine[0] = CreateStripMatrix(halfWidth, halfHeight, borderThickness, z, stripDepth, edgeY: 1f);
        combine[1] = CreateStripMatrix(halfWidth, halfHeight, borderThickness, z, stripDepth, edgeY: -1f);
        combine[2] = CreateStripMatrix(halfWidth, halfHeight, borderThickness, z, stripDepth, edgeX: -1f);
        combine[3] = CreateStripMatrix(halfWidth, halfHeight, borderThickness, z, stripDepth, edgeX: 1f);

        var mesh = new Mesh { name = "CardBorderFrame" };
        mesh.CombineMeshes(combine, mergeSubMeshes: true, useMatrices: true);
        return mesh;
    }

    static CombineInstance CreateStripMatrix(
        float halfWidth,
        float halfHeight,
        float borderThickness,
        float z,
        float stripDepth,
        float edgeX = 0f,
        float edgeY = 0f)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Vector3 scale;
        Vector3 position;

        if (edgeY != 0f)
        {
            scale = new Vector3(
                halfWidth * 2f + borderThickness * 2f,
                borderThickness,
                stripDepth);
            position = new Vector3(0f, edgeY * (halfHeight + borderThickness * 0.5f), z);
        }
        else
        {
            scale = new Vector3(
                borderThickness,
                halfHeight * 2f,
                stripDepth);
            position = new Vector3(edgeX * (halfWidth + borderThickness * 0.5f), 0f, z);
        }

        var combine = new CombineInstance
        {
            mesh = cube.GetComponent<MeshFilter>().sharedMesh,
            transform = Matrix4x4.TRS(position, Quaternion.identity, scale),
        };

        Object.DestroyImmediate(cube);
        return combine;
    }
}

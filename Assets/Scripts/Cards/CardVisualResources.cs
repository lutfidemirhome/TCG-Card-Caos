using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Shared mesh and materials for all world cards. Keeps draw-call and memory cost flat
/// as card count grows toward thousands.
/// </summary>
static class CardVisualResources
{
    static readonly Color BorderColor = new Color(0.85f, 0.72f, 0.2f);
    static readonly Color InteractionOutlineColor = new Color(1f, 0.88f, 0.12f);

    static Mesh _cardMesh;
    static Mesh _interactionBorderFrameMesh;
    static Material _borderMaterial;
    static Material _outlineMaterial;
    static readonly Dictionary<int, Material> FaceMaterials = new Dictionary<int, Material>();

    public static Mesh CardMesh
    {
        get
        {
            EnsureInitialized();
            return _cardMesh;
        }
    }

    public static Material BorderMaterial
    {
        get
        {
            EnsureInitialized();
            return _borderMaterial;
        }
    }

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

    public static Material GetFaceMaterial(int paletteIndex)
    {
        return GetFaceMaterial(CardPalette.GetColor(paletteIndex));
    }

    public static Material GetFaceMaterial(Color color)
    {
        EnsureInitialized();

        int key = ColorToKey(color);
        if (!FaceMaterials.TryGetValue(key, out Material material))
        {
            material = RuntimeMaterialUtility.CreateSharedColorMaterial(color, enableInstancing: true);
            FaceMaterials[key] = material;
        }

        return material;
    }

    static void EnsureInitialized()
    {
        if (_cardMesh != null && _interactionBorderFrameMesh != null && _borderMaterial != null && _outlineMaterial != null)
            return;

        _cardMesh ??= BuildCombinedCardMesh();
        _interactionBorderFrameMesh ??= BuildInteractionBorderFrameMesh();
        _borderMaterial ??= RuntimeMaterialUtility.CreateSharedColorMaterial(BorderColor, enableInstancing: true);
        _outlineMaterial ??= RuntimeMaterialUtility.CreateUnlitMaterial(
            InteractionOutlineColor,
            enableInstancing: true,
            renderQueue: (int)RenderQueue.Geometry + 1);
    }

    static Mesh BuildInteractionBorderFrameMesh()
    {
        float halfWidth = CardDimensions.Width * 0.5f;
        float halfHeight = CardDimensions.Height * 0.5f;
        float borderThickness = CardDimensions.InteractionOutlineThickness;
        float y = CardDimensions.Thickness * 0.65f;
        float verticalSize = CardDimensions.Thickness * 0.35f;

        var combine = new CombineInstance[4];
        combine[0] = CreateStripMatrix(halfWidth, halfHeight, borderThickness, y, verticalSize, edgeZ: 1f);
        combine[1] = CreateStripMatrix(halfWidth, halfHeight, borderThickness, y, verticalSize, edgeZ: -1f);
        combine[2] = CreateStripMatrix(halfWidth, halfHeight, borderThickness, y, verticalSize, edgeX: -1f);
        combine[3] = CreateStripMatrix(halfWidth, halfHeight, borderThickness, y, verticalSize, edgeX: 1f);

        var mesh = new Mesh { name = "CardInteractionBorderFrame" };
        mesh.CombineMeshes(combine, mergeSubMeshes: true, useMatrices: true);
        return mesh;
    }

    static CombineInstance CreateStripMatrix(
        float halfWidth,
        float halfHeight,
        float borderThickness,
        float y,
        float verticalSize,
        float edgeX = 0f,
        float edgeZ = 0f)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Vector3 scale;
        Vector3 position;

        if (edgeZ != 0f)
        {
            scale = new Vector3(
                CardDimensions.Width + borderThickness * 2f,
                verticalSize,
                borderThickness);
            position = new Vector3(0f, y, edgeZ * (halfHeight + borderThickness * 0.5f));
        }
        else
        {
            scale = new Vector3(
                borderThickness,
                verticalSize,
                CardDimensions.Height);
            position = new Vector3(edgeX * (halfWidth + borderThickness * 0.5f), y, 0f);
        }

        var combine = new CombineInstance
        {
            mesh = cube.GetComponent<MeshFilter>().sharedMesh,
            transform = Matrix4x4.TRS(position, Quaternion.identity, scale),
        };

        Object.DestroyImmediate(cube);
        return combine;
    }

    static Mesh BuildCombinedCardMesh()
    {
        float width = CardDimensions.Width;
        float height = CardDimensions.Height;
        float thickness = CardDimensions.Thickness;

        var border = GameObject.CreatePrimitive(PrimitiveType.Cube);
        border.transform.localScale = new Vector3(width * 1.04f, thickness, height * 1.04f);

        var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
        face.transform.localScale = new Vector3(width, thickness * 1.2f, height);

        var combine = new CombineInstance[2];
        combine[0].mesh = border.GetComponent<MeshFilter>().sharedMesh;
        combine[0].transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, border.transform.localScale);
        combine[1].mesh = face.GetComponent<MeshFilter>().sharedMesh;
        combine[1].transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, face.transform.localScale);

        var mesh = new Mesh { name = "CardCombined" };
        mesh.CombineMeshes(combine, mergeSubMeshes: false, useMatrices: true);

        Object.DestroyImmediate(border);
        Object.DestroyImmediate(face);

        return mesh;
    }

    static int ColorToKey(Color color)
    {
        var c32 = (Color32)color;
        return c32.r | (c32.g << 8) | (c32.b << 16) | (c32.a << 24);
    }
}

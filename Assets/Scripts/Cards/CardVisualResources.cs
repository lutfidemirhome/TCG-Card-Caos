using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared mesh and materials for all world cards. Keeps draw-call and memory cost flat
/// as card count grows toward thousands.
/// </summary>
static class CardVisualResources
{
    static readonly Color BorderColor = new Color(0.85f, 0.72f, 0.2f);

    static Mesh _cardMesh;
    static Material _borderMaterial;
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
        if (_cardMesh != null && _borderMaterial != null)
            return;

        _cardMesh ??= BuildCombinedCardMesh();
        _borderMaterial ??= RuntimeMaterialUtility.CreateSharedColorMaterial(BorderColor, enableInstancing: true);
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

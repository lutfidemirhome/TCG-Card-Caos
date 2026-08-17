using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Outline meshes for booster packs. Built to hug the actual imported pack mesh's own
/// measured bounds (not the trading-card mesh), so the frame sits flush against whatever
/// pack model/scale is in use. Kept separate from <see cref="CardVisualResources"/> —
/// card outlines are untouched by pack tuning.
/// </summary>
static class PackVisualResources
{
    const float InteractionBorderThicknessPercent = 0.07f;
    const float HandSelectionBorderThicknessPercent = 0.024f;

    static readonly Dictionary<long, Mesh> InteractionMeshCache = new Dictionary<long, Mesh>();
    static readonly Dictionary<long, Mesh> HandSelectionMeshCache = new Dictionary<long, Mesh>();

#if UNITY_EDITOR
    [UnityEditor.InitializeOnEnterPlayMode]
    static void ResetPlayModeCaches(UnityEditor.EnterPlayModeOptions options)
    {
        InteractionMeshCache.Clear();
        HandSelectionMeshCache.Clear();
    }
#endif

    public static Mesh GetInteractionBorderMesh(Vector3 meshSize)
    {
        float borderThickness = Mathf.Max(meshSize.x, 0.0001f) * InteractionBorderThicknessPercent;
        return GetOrBuild(InteractionMeshCache, meshSize, borderThickness);
    }

    public static Mesh GetHandSelectionBorderMesh(Vector3 meshSize)
    {
        float borderThickness = Mathf.Max(meshSize.x, 0.0001f) * HandSelectionBorderThicknessPercent;
        return GetOrBuild(HandSelectionMeshCache, meshSize, borderThickness);
    }

    static Mesh GetOrBuild(Dictionary<long, Mesh> cache, Vector3 size, float borderThickness)
    {
        long key = HashKey(size, borderThickness);
        if (cache.TryGetValue(key, out Mesh mesh) && mesh != null)
            return mesh;

        mesh = BuildBorderFrameMesh(size, borderThickness);
        cache[key] = mesh;
        return mesh;
    }

    static long HashKey(Vector3 size, float borderThickness)
    {
        long x = Mathf.RoundToInt(size.x * 1000000f);
        long y = Mathf.RoundToInt(size.y * 1000000f);
        long z = Mathf.RoundToInt(size.z * 1000000f);
        long t = Mathf.RoundToInt(borderThickness * 1000000f);
        long key = x;
        key = (key * 1000003L) ^ y;
        key = (key * 1000003L) ^ z;
        key = (key * 1000003L) ^ t;
        return key;
    }

    /// <summary>
    /// Rectangular picture-frame ring hugging the pack's own [-halfWidth, halfWidth] x
    /// [-halfHeight, halfHeight] footprint, with the same thin-extrusion-near-surface trick
    /// as the card border mesh so it sits flush without z-fighting.
    /// </summary>
    static Mesh BuildBorderFrameMesh(Vector3 size, float borderThickness)
    {
        float halfWidth = Mathf.Max(size.x, 0.0001f) * 0.5f;
        float halfHeight = Mathf.Max(size.y, 0.0001f) * 0.5f;
        float halfThickness = Mathf.Max(size.z, 0.0004f) * 0.5f;
        float z = halfThickness * 0.65f;
        float stripDepth = Mathf.Max(halfThickness * 0.35f * 2f, borderThickness);

        var innerLoop = new List<Vector2>
        {
            new Vector2(halfWidth, -halfHeight),
            new Vector2(halfWidth, halfHeight),
            new Vector2(-halfWidth, halfHeight),
            new Vector2(-halfWidth, -halfHeight),
        };
        var outerLoop = new List<Vector2>
        {
            new Vector2(halfWidth + borderThickness, -halfHeight - borderThickness),
            new Vector2(halfWidth + borderThickness, halfHeight + borderThickness),
            new Vector2(-halfWidth - borderThickness, halfHeight + borderThickness),
            new Vector2(-halfWidth - borderThickness, -halfHeight - borderThickness),
        };

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        ExtrudeRing(vertices, triangles, innerLoop, outerLoop, z, stripDepth * 0.5f);

        var mesh = new Mesh { name = "PackBorderFrame" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void ExtrudeRing(
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector2> innerLoop,
        List<Vector2> outerLoop,
        float centerZ,
        float halfDepth)
    {
        int count = innerLoop.Count;
        float zBack = centerZ - halfDepth;
        float zFront = centerZ + halfDepth;

        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            Vector2 inner = innerLoop[i];
            Vector2 outer = outerLoop[i];
            Vector2 innerNext = innerLoop[next];
            Vector2 outerNext = outerLoop[next];

            AddQuad(vertices, triangles,
                new Vector3(outer.x, outer.y, zFront),
                new Vector3(outerNext.x, outerNext.y, zFront),
                new Vector3(innerNext.x, innerNext.y, zFront),
                new Vector3(inner.x, inner.y, zFront));

            AddQuad(vertices, triangles,
                new Vector3(outer.x, outer.y, zBack),
                new Vector3(inner.x, inner.y, zBack),
                new Vector3(innerNext.x, innerNext.y, zBack),
                new Vector3(outerNext.x, outerNext.y, zBack));

            AddQuad(vertices, triangles,
                new Vector3(outer.x, outer.y, zBack),
                new Vector3(outer.x, outer.y, zFront),
                new Vector3(outerNext.x, outerNext.y, zFront),
                new Vector3(outerNext.x, outerNext.y, zBack));

            AddQuad(vertices, triangles,
                new Vector3(inner.x, inner.y, zBack),
                new Vector3(innerNext.x, innerNext.y, zBack),
                new Vector3(innerNext.x, innerNext.y, zFront),
                new Vector3(inner.x, inner.y, zFront));
        }
    }

    static void AddQuad(
        List<Vector3> vertices,
        List<int> triangles,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }
}

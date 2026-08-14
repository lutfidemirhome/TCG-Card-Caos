using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Shared outline meshes/materials for card interaction and hand selection.
/// Built in mesh-local space and parented under CardVisual.
/// </summary>
static class CardVisualResources
{
    static Mesh _interactionBorderFrameMesh;
    static Mesh _handSelectionBorderFrameMesh;
    static Material _outlineMaterial;
    static Material _handSelectionOutlineMaterial;
    static Material _shelfPlacementOutlineMaterial;
    static Material _shelfCorrectOutlineMaterial;
    static Material _shelfIncorrectOutlineMaterial;

    const int CornerSegments = 12;

    public static Material InteractionOutlineMaterial
    {
        get
        {
            EnsureInitialized();
            return _outlineMaterial;
        }
    }

    public static Material ShelfPlacementOutlineMaterial
    {
        get
        {
            EnsureInitialized();
            return _shelfPlacementOutlineMaterial;
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

    public static Material ShelfCorrectOutlineMaterial
    {
        get
        {
            EnsureInitialized();
            return _shelfCorrectOutlineMaterial;
        }
    }

    public static Material ShelfIncorrectOutlineMaterial
    {
        get
        {
            EnsureInitialized();
            return _shelfIncorrectOutlineMaterial;
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
        _outlineMaterial = null;
        _handSelectionOutlineMaterial = null;
        _shelfPlacementOutlineMaterial = null;
        _shelfCorrectOutlineMaterial = null;
        _shelfIncorrectOutlineMaterial = null;
    }

    public static void ApplyOutlineSettings(CardOutlineSettings settings)
    {
        if (settings == null)
            return;

        ApplyPalette(settings.GetPalette());
    }

    public static void ApplyPalette(CardOutlineSettings.Palette palette)
    {
        EnsureInitialized();
        SetMaterialColor(_outlineMaterial, palette.cardHover);
        SetMaterialColor(_handSelectionOutlineMaterial, palette.handSelection);
        SetMaterialColor(_shelfPlacementOutlineMaterial, palette.shelfPlacement);
        SetMaterialColor(_shelfCorrectOutlineMaterial, palette.shelfCorrect);
        SetMaterialColor(_shelfIncorrectOutlineMaterial, palette.shelfIncorrect);
    }

    static void EnsureInitialized()
    {
        CardArtLibrary.EnsureLoaded();

        if (_interactionBorderFrameMesh != null && _handSelectionBorderFrameMesh != null
            && _outlineMaterial != null && _handSelectionOutlineMaterial != null
            && _shelfPlacementOutlineMaterial != null
            && _shelfCorrectOutlineMaterial != null && _shelfIncorrectOutlineMaterial != null)
            return;

        _interactionBorderFrameMesh ??= BuildBorderFrameMesh(CardDimensions.InteractionOutlineThickness);
        _handSelectionBorderFrameMesh ??= BuildBorderFrameMesh(CardDimensions.HandSelectionOutlineThickness);
        _outlineMaterial ??= RuntimeMaterialUtility.CreateUnlitMaterial(
            CardOutlineSettings.GetPaletteOrDefaults().cardHover,
            enableInstancing: true,
            renderQueue: (int)RenderQueue.Geometry + 1);
        _handSelectionOutlineMaterial ??= RuntimeMaterialUtility.CreateUnlitMaterial(
            CardOutlineSettings.GetPaletteOrDefaults().handSelection,
            enableInstancing: true,
            renderQueue: (int)RenderQueue.Geometry + 2);
        _shelfPlacementOutlineMaterial ??= RuntimeMaterialUtility.CreateUnlitMaterial(
            CardOutlineSettings.GetPaletteOrDefaults().shelfPlacement,
            enableInstancing: true,
            renderQueue: (int)RenderQueue.Geometry + 1);
        _shelfCorrectOutlineMaterial ??= RuntimeMaterialUtility.CreateUnlitMaterial(
            CardOutlineSettings.GetPaletteOrDefaults().shelfCorrect,
            enableInstancing: true,
            renderQueue: (int)RenderQueue.Geometry + 1);
        _shelfIncorrectOutlineMaterial ??= RuntimeMaterialUtility.CreateUnlitMaterial(
            CardOutlineSettings.GetPaletteOrDefaults().shelfIncorrect,
            enableInstancing: true,
            renderQueue: (int)RenderQueue.Geometry + 1);
    }

    static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    static Mesh BuildBorderFrameMesh(float borderThickness)
    {
        Bounds bounds = CardArtLibrary.MeshBounds;
        float halfWidth = bounds.extents.x;
        float halfHeight = bounds.extents.y;
        float halfThickness = bounds.extents.z;
        float z = halfThickness * 0.65f;
        float stripDepth = Mathf.Max(halfThickness * 0.35f * 2f, borderThickness);
        float cornerRadius = CardArtLibrary.MeshCornerRadius;

        List<Vector2> innerLoop = BuildRoundedRectPerimeter(halfWidth, halfHeight, cornerRadius, CornerSegments);
        List<Vector2> outerLoop = BuildRoundedRectPerimeter(
            halfWidth + borderThickness,
            halfHeight + borderThickness,
            cornerRadius + borderThickness,
            CornerSegments);

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        ExtrudeRing(vertices, triangles, innerLoop, outerLoop, z, stripDepth * 0.5f);

        var mesh = new Mesh { name = "CardRoundedBorderFrame" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static List<Vector2> BuildRoundedRectPerimeter(float halfWidth, float halfHeight, float cornerRadius, int cornerSegments)
    {
        cornerRadius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(halfWidth, halfHeight) - 0.0001f);
        float straightX = halfWidth - cornerRadius;
        float straightY = halfHeight - cornerRadius;

        var points = new List<Vector2>(cornerSegments * 4);
        AppendCornerArc(points, straightX, -straightY, cornerRadius, 270f, 360f, cornerSegments, skipFirst: false);
        AppendCornerArc(points, straightX, straightY, cornerRadius, 0f, 90f, cornerSegments, skipFirst: true);
        AppendCornerArc(points, -straightX, straightY, cornerRadius, 90f, 180f, cornerSegments, skipFirst: true);
        AppendCornerArc(points, -straightX, -straightY, cornerRadius, 180f, 270f, cornerSegments, skipFirst: true);
        return points;
    }

    static void AppendCornerArc(
        List<Vector2> points,
        float centerX,
        float centerY,
        float radius,
        float startDegrees,
        float endDegrees,
        int segments,
        bool skipFirst)
    {
        for (int i = 0; i < segments; i++)
        {
            if (skipFirst && i == 0)
                continue;

            float t = i / (float)segments;
            float degrees = Mathf.Lerp(startDegrees, endDegrees, t);
            float radians = degrees * Mathf.Deg2Rad;
            points.Add(new Vector2(
                centerX + radius * Mathf.Cos(radians),
                centerY + radius * Mathf.Sin(radians)));
        }
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

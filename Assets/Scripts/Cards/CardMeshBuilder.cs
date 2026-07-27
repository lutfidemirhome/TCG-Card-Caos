using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a lightweight trading-card mesh (~100 triangles) that matches the yzma silhouette.
/// UVs are copied from the source mesh surface so art alignment stays identical.
/// </summary>
public static class CardMeshBuilder
{
    public const int DefaultCornerSegments = 8;

    public static Mesh CreateTradingCardMesh(
        float halfWidth,
        float halfHeight,
        float halfThickness,
        float cornerRadius,
        int cornerSegments = DefaultCornerSegments,
        Mesh uvReferenceMesh = null,
        bool includeEdgeGeometry = true)
    {
        FaceUvProjector uvProjector = uvReferenceMesh != null ? new FaceUvProjector(uvReferenceMesh) : null;

        List<Vector2> perimeter = BuildRoundedRectPerimeter(
            halfWidth,
            halfHeight,
            cornerRadius,
            cornerSegments);

        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var frontTriangles = new List<int>();
        var backTriangles = new List<int>();

        int centerFront = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, halfThickness));
        uvs.Add(SampleUv(uvProjector, 0f, 0f, halfWidth, halfHeight, frontFace: true));

        var frontRing = new int[perimeter.Count];
        for (int i = 0; i < perimeter.Count; i++)
        {
            Vector2 point = perimeter[i];
            frontRing[i] = vertices.Count;
            vertices.Add(new Vector3(point.x, point.y, halfThickness));
            uvs.Add(SampleUv(uvProjector, point.x, point.y, halfWidth, halfHeight, frontFace: true));
        }

        for (int i = 0; i < perimeter.Count; i++)
        {
            int next = (i + 1) % perimeter.Count;
            frontTriangles.Add(centerFront);
            frontTriangles.Add(frontRing[i]);
            frontTriangles.Add(frontRing[next]);
        }

        int centerBack = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, -halfThickness));
        uvs.Add(SampleUv(uvProjector, 0f, 0f, halfWidth, halfHeight, frontFace: false));

        var backRing = new int[perimeter.Count];
        for (int i = 0; i < perimeter.Count; i++)
        {
            Vector2 point = perimeter[i];
            backRing[i] = vertices.Count;
            vertices.Add(new Vector3(point.x, point.y, -halfThickness));
            uvs.Add(SampleUv(uvProjector, point.x, point.y, halfWidth, halfHeight, frontFace: false));
        }

        for (int i = 0; i < perimeter.Count; i++)
        {
            int next = (i + 1) % perimeter.Count;
            backTriangles.Add(centerBack);
            backTriangles.Add(backRing[next]);
            backTriangles.Add(backRing[i]);
        }

        if (includeEdgeGeometry)
        {
            for (int i = 0; i < perimeter.Count; i++)
            {
                int next = (i + 1) % perimeter.Count;
                int frontA = frontRing[i];
                int frontB = frontRing[next];
                int backA = backRing[i];
                int backB = backRing[next];

                backTriangles.Add(frontA);
                backTriangles.Add(backA);
                backTriangles.Add(backB);

                backTriangles.Add(frontA);
                backTriangles.Add(backB);
                backTriangles.Add(frontB);
            }
        }

        var mesh = new Mesh { name = "TradingCardMesh" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(frontTriangles, 0);
        mesh.SetTriangles(backTriangles, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Vector2 SampleUv(
        FaceUvProjector uvProjector,
        float x,
        float y,
        float halfWidth,
        float halfHeight,
        bool frontFace)
    {
        if (uvProjector != null)
            return uvProjector.Sample(x, y, frontFace);

        return new Vector2(
            Mathf.Clamp01((x + halfWidth) / (halfWidth * 2f)),
            Mathf.Clamp01((y + halfHeight) / (halfHeight * 2f)));
    }

    /// <summary>
    /// Single front-face quad for GPU-instanced ground cards (2 tris).
    /// UVs are linearly mapped from the yzma front-face art region (bevel padding excluded).
    /// </summary>
    public static Mesh CreateInstancedGroundCardMesh(Mesh referenceMesh)
    {
        PlanarFaceMapping mapping = ExtractFrontFacePlanarMapping(referenceMesh);
        if (!mapping.Valid)
            return CreateFallbackGroundQuad(referenceMesh.bounds);

        float frontZ = referenceMesh.bounds.max.z;
        var vertices = new Vector3[4];
        var uvs = new Vector2[4];

        vertices[0] = new Vector3(mapping.MinX, mapping.MinY, frontZ);
        vertices[1] = new Vector3(mapping.MaxX, mapping.MinY, frontZ);
        vertices[2] = new Vector3(mapping.MaxX, mapping.MaxY, frontZ);
        vertices[3] = new Vector3(mapping.MinX, mapping.MaxY, frontZ);

        uvs[0] = mapping.Map(mapping.MinX, mapping.MinY);
        uvs[1] = mapping.Map(mapping.MaxX, mapping.MinY);
        uvs[2] = mapping.Map(mapping.MaxX, mapping.MaxY);
        uvs[3] = mapping.Map(mapping.MinX, mapping.MaxY);

        var mesh = new Mesh { name = "InstancedGroundCardMesh" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.subMeshCount = 1;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh CreateFallbackGroundQuad(Bounds bounds)
    {
        float frontZ = bounds.max.z;
        var vertices = new[]
        {
            new Vector3(bounds.min.x, bounds.min.y, frontZ),
            new Vector3(bounds.max.x, bounds.min.y, frontZ),
            new Vector3(bounds.max.x, bounds.max.y, frontZ),
            new Vector3(bounds.min.x, bounds.max.y, frontZ),
        };
        var uvs = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };

        var mesh = new Mesh { name = "InstancedGroundCardMesh" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.subMeshCount = 1;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static PlanarFaceMapping ExtractFrontFacePlanarMapping(Mesh mesh, float innerMarginPercent = 0.08f)
    {
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        Bounds bounds = mesh.bounds;
        float targetZ = bounds.max.z;
        float zTolerance = Mathf.Max(bounds.extents.z * 0.55f, 0.00001f);

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];
            if (Mathf.Abs(vertex.z - targetZ) > zTolerance)
                continue;

            minX = Mathf.Min(minX, vertex.x);
            maxX = Mathf.Max(maxX, vertex.x);
            minY = Mathf.Min(minY, vertex.y);
            maxY = Mathf.Max(maxY, vertex.y);
        }

        if (minX >= maxX || minY >= maxY)
            return default;

        float marginX = (maxX - minX) * innerMarginPercent;
        float marginY = (maxY - minY) * innerMarginPercent;
        float innerMinX = minX + marginX;
        float innerMaxX = maxX - marginX;
        float innerMinY = minY + marginY;
        float innerMaxY = maxY - marginY;

        float minU = float.PositiveInfinity;
        float maxU = float.NegativeInfinity;
        float minV = float.PositiveInfinity;
        float maxV = float.NegativeInfinity;
        int sampleCount = 0;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];
            if (Mathf.Abs(vertex.z - targetZ) > zTolerance)
                continue;

            if (vertex.x < innerMinX || vertex.x > innerMaxX || vertex.y < innerMinY || vertex.y > innerMaxY)
                continue;

            Vector2 uv = uvs[i];
            minU = Mathf.Min(minU, uv.x);
            maxU = Mathf.Max(maxU, uv.x);
            minV = Mathf.Min(minV, uv.y);
            maxV = Mathf.Max(maxV, uv.y);
            sampleCount++;
        }

        if (sampleCount == 0)
            return default;

        return new PlanarFaceMapping
        {
            MinX = minX,
            MaxX = maxX,
            MinY = minY,
            MaxY = maxY,
            MinU = minU,
            MaxU = maxU,
            MinV = minV,
            MaxV = maxV,
            Valid = true,
        };
    }

    public struct PlanarFaceMapping
    {
        public float MinX;
        public float MaxX;
        public float MinY;
        public float MaxY;
        public float MinU;
        public float MaxU;
        public float MinV;
        public float MaxV;
        public bool Valid;

        public Vector2 Map(float x, float y)
        {
            float u = Mathf.Lerp(MinU, MaxU, Mathf.InverseLerp(MinX, MaxX, x));
            float v = Mathf.Lerp(MinV, MaxV, Mathf.InverseLerp(MinY, MaxY, y));
            return new Vector2(u, v);
        }
    }

    public static float EstimateCornerRadius(Mesh sourceMesh, Bounds bounds)
    {
        if (sourceMesh == null)
            return Mathf.Min(bounds.extents.x, bounds.extents.y) * 0.055f;

        Vector3[] vertices = sourceMesh.vertices;
        float halfW = bounds.extents.x;
        float halfH = bounds.extents.y;
        float frontZ = bounds.max.z;
        float zTolerance = bounds.extents.z * 0.6f;
        float edgeBand = Mathf.Max(halfH, halfW) * 0.015f;

        float EstimateFromCorner(float cornerX, float cornerY, bool usePositiveX, bool usePositiveY)
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

                if (Mathf.Abs(v.y - cornerY) > edgeBand)
                    continue;

                estimate = Mathf.Min(estimate, Mathf.Abs(cornerX - v.x));
            }

            return estimate;
        }

        float topRight = EstimateFromCorner(halfW, halfH, usePositiveX: true, usePositiveY: true);
        float topLeft = EstimateFromCorner(-halfW, halfH, usePositiveX: false, usePositiveY: true);
        float bottomRight = EstimateFromCorner(halfW, -halfH, usePositiveX: true, usePositiveY: false);
        float bottomLeft = EstimateFromCorner(-halfW, -halfH, usePositiveX: false, usePositiveY: false);

        float radius = Mathf.Min(topRight, topLeft, bottomRight, bottomLeft);
        if (radius > 0.0005f && radius < Mathf.Min(halfW, halfH) * 0.35f)
            return radius;

        return Mathf.Min(halfW, halfH) * 0.055f;
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

    sealed class FaceUvProjector
    {
        readonly PlanarFaceMapping _front;
        readonly PlanarFaceMapping _back;

        public FaceUvProjector(Mesh mesh)
        {
            _front = ExtractFrontFacePlanarMapping(mesh);
            _back = ExtractBackFacePlanarMapping(mesh);
        }

        public Vector2 Sample(float x, float y, bool frontFace)
        {
            PlanarFaceMapping mapping = frontFace ? _front : _back;
            return mapping.Valid ? mapping.Map(x, y) : Vector2.zero;
        }

        static PlanarFaceMapping ExtractBackFacePlanarMapping(Mesh mesh, float innerMarginPercent = 0.08f)
        {
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            Bounds bounds = mesh.bounds;
            float targetZ = bounds.min.z;
            float zTolerance = Mathf.Max(bounds.extents.z * 0.55f, 0.00001f);

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                if (Mathf.Abs(vertex.z - targetZ) > zTolerance)
                    continue;

                minX = Mathf.Min(minX, vertex.x);
                maxX = Mathf.Max(maxX, vertex.x);
                minY = Mathf.Min(minY, vertex.y);
                maxY = Mathf.Max(maxY, vertex.y);
            }

            if (minX >= maxX || minY >= maxY)
                return default;

            float marginX = (maxX - minX) * innerMarginPercent;
            float marginY = (maxY - minY) * innerMarginPercent;
            float innerMinX = minX + marginX;
            float innerMaxX = maxX - marginX;
            float innerMinY = minY + marginY;
            float innerMaxY = maxY - marginY;

            float minU = float.PositiveInfinity;
            float maxU = float.NegativeInfinity;
            float minV = float.PositiveInfinity;
            float maxV = float.NegativeInfinity;
            int sampleCount = 0;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                if (Mathf.Abs(vertex.z - targetZ) > zTolerance)
                    continue;

                if (vertex.x < innerMinX || vertex.x > innerMaxX || vertex.y < innerMinY || vertex.y > innerMaxY)
                    continue;

                Vector2 uv = uvs[i];
                minU = Mathf.Min(minU, uv.x);
                maxU = Mathf.Max(maxU, uv.x);
                minV = Mathf.Min(minV, uv.y);
                maxV = Mathf.Max(maxV, uv.y);
                sampleCount++;
            }

            if (sampleCount == 0)
                return default;

            return new PlanarFaceMapping
            {
                MinX = minX,
                MaxX = maxX,
                MinY = minY,
                MaxY = maxY,
                MinU = minU,
                MaxU = maxU,
                MinV = minV,
                MaxV = maxV,
                Valid = true,
            };
        }
    }
}

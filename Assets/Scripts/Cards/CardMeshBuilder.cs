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
    /// UVs are fitted from interior flat-face samples so bevel/black padding is excluded.
    /// </summary>
    public static Mesh CreateInstancedGroundCardMesh(Mesh referenceMesh)
    {
        if (!TryFitFrontFaceUv(referenceMesh, out Bounds xyBounds, out float frontZ, out UvPlaneFit fit))
            return CreateFallbackGroundQuad(referenceMesh.bounds);

        float minX = xyBounds.min.x;
        float maxX = xyBounds.max.x;
        float minY = xyBounds.min.y;
        float maxY = xyBounds.max.y;

        var vertices = new[]
        {
            new Vector3(minX, minY, frontZ),
            new Vector3(maxX, minY, frontZ),
            new Vector3(maxX, maxY, frontZ),
            new Vector3(minX, maxY, frontZ),
        };
        var uvs = new[]
        {
            fit.Evaluate(minX, minY),
            fit.Evaluate(maxX, minY),
            fit.Evaluate(maxX, maxY),
            fit.Evaluate(minX, maxY),
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

    /// <summary>
    /// Fits U = ax+by+c, V = dx+ey+f from interior front-face verts (avoids bevel black padding).
    /// </summary>
    static bool TryFitFrontFaceUv(Mesh mesh, out Bounds xyBounds, out float frontZ, out UvPlaneFit fit)
    {
        xyBounds = default;
        frontZ = 0f;
        fit = default;

        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        Bounds bounds = mesh.bounds;
        frontZ = bounds.max.z;
        float zTolerance = Mathf.Max(bounds.extents.z * 0.35f, 0.00001f);
        bool[] frontSubmeshVertices = BuildFrontSubmeshVertexMask(mesh);

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        var samples = new List<Vector3>(4096);

        for (int i = 0; i < vertices.Length; i++)
        {
            if (!IsFrontFaceVertex(vertices[i], frontZ, zTolerance, frontSubmeshVertices, i))
                continue;

            Vector3 vertex = vertices[i];
            minX = Mathf.Min(minX, vertex.x);
            maxX = Mathf.Max(maxX, vertex.x);
            minY = Mathf.Min(minY, vertex.y);
            maxY = Mathf.Max(maxY, vertex.y);
            samples.Add(new Vector3(vertex.x, vertex.y, i));
        }

        if (samples.Count < 8 || minX >= maxX || minY >= maxY)
            return false;

        xyBounds = new Bounds(
            new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, frontZ),
            new Vector3(maxX - minX, maxY - minY, 0f));

        // Keep only interior samples so edge bevel UVs (black padding) do not skew the fit.
        float innerMinX = Mathf.Lerp(minX, maxX, 0.12f);
        float innerMaxX = Mathf.Lerp(minX, maxX, 0.88f);
        float innerMinY = Mathf.Lerp(minY, maxY, 0.12f);
        float innerMaxY = Mathf.Lerp(minY, maxY, 0.88f);

        // Normal equations for least squares: [x y 1] * coeffs = uv
        double suu = 0, suv = 0, su = 0, svv = 0, sv = 0, s1 = 0;
        double sxu = 0, sxv = 0, syu = 0, syv = 0, s1u = 0, s1v = 0;
        int used = 0;

        for (int i = 0; i < samples.Count; i++)
        {
            float x = samples[i].x;
            float y = samples[i].y;
            if (x < innerMinX || x > innerMaxX || y < innerMinY || y > innerMaxY)
                continue;

            int vertexIndex = (int)samples[i].z;
            Vector2 uv = uvs[vertexIndex];
            double xd = x;
            double yd = y;
            double ud = uv.x;
            double vd = uv.y;

            suu += xd * xd;
            suv += xd * yd;
            su += xd;
            svv += yd * yd;
            sv += yd;
            s1 += 1.0;
            sxu += xd * ud;
            sxv += xd * vd;
            syu += yd * ud;
            syv += yd * vd;
            s1u += ud;
            s1v += vd;
            used++;
        }

        if (used < 8)
            return false;

        if (!TrySolve3x3(suu, suv, su, suv, svv, sv, su, sv, s1, sxu, syu, s1u, out double a, out double b, out double c))
            return false;

        if (!TrySolve3x3(suu, suv, su, suv, svv, sv, su, sv, s1, sxv, syv, s1v, out double d, out double e, out double f))
            return false;

        fit = new UvPlaneFit
        {
            Au = (float)a,
            Bu = (float)b,
            Cu = (float)c,
            Av = (float)d,
            Bv = (float)e,
            Cv = (float)f,
        };
        return true;
    }

    static bool TrySolve3x3(
        double a00, double a01, double a02,
        double a10, double a11, double a12,
        double a20, double a21, double a22,
        double b0, double b1, double b2,
        out double x0, out double x1, out double x2)
    {
        x0 = x1 = x2 = 0;
        double det =
            a00 * (a11 * a22 - a12 * a21)
            - a01 * (a10 * a22 - a12 * a20)
            + a02 * (a10 * a21 - a11 * a20);

        if (System.Math.Abs(det) < 1e-12)
            return false;

        x0 = (
            b0 * (a11 * a22 - a12 * a21)
            - a01 * (b1 * a22 - a12 * b2)
            + a02 * (b1 * a21 - a11 * b2)) / det;
        x1 = (
            a00 * (b1 * a22 - a12 * b2)
            - b0 * (a10 * a22 - a12 * a20)
            + a02 * (a10 * b2 - b1 * a20)) / det;
        x2 = (
            a00 * (a11 * b2 - b1 * a21)
            - a01 * (a10 * b2 - b1 * a20)
            + b0 * (a10 * a21 - a11 * a20)) / det;
        return true;
    }

    struct UvPlaneFit
    {
        public float Au;
        public float Bu;
        public float Cu;
        public float Av;
        public float Bv;
        public float Cv;

        public Vector2 Evaluate(float x, float y)
        {
            return new Vector2(Au * x + Bu * y + Cu, Av * x + Bv * y + Cv);
        }
    }

    static bool[] BuildFrontSubmeshVertexMask(Mesh mesh)
    {
        if (mesh.subMeshCount <= 0)
            return null;

        var mask = new bool[mesh.vertexCount];
        int[] indices = mesh.GetTriangles(0);
        for (int i = 0; i < indices.Length; i++)
            mask[indices[i]] = true;

        return mask;
    }

    static bool IsFrontFaceVertex(Vector3 vertex, float targetZ, float zTolerance, bool[] frontSubmeshVertices, int index)
    {
        if (frontSubmeshVertices != null && !frontSubmeshVertices[index])
            return false;

        return Mathf.Abs(vertex.z - targetZ) <= zTolerance;
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

    public static PlanarFaceMapping ExtractFrontFacePlanarMapping(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;
        Bounds bounds = mesh.bounds;
        float targetZ = bounds.max.z;
        float zTolerance = Mathf.Max(bounds.extents.z * 0.55f, 0.00001f);
        bool[] frontSubmeshVertices = BuildFrontSubmeshVertexMask(mesh);

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        float minU = float.PositiveInfinity;
        float maxU = float.NegativeInfinity;
        float minV = float.PositiveInfinity;
        float maxV = float.NegativeInfinity;
        int sampleCount = 0;

        for (int i = 0; i < vertices.Length; i++)
        {
            if (!IsFrontFaceVertex(vertices[i], targetZ, zTolerance, frontSubmeshVertices, i))
                continue;

            Vector3 vertex = vertices[i];
            Vector2 uv = uvs[i];
            minX = Mathf.Min(minX, vertex.x);
            maxX = Mathf.Max(maxX, vertex.x);
            minY = Mathf.Min(minY, vertex.y);
            maxY = Mathf.Max(maxY, vertex.y);
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

        static PlanarFaceMapping ExtractBackFacePlanarMapping(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            Bounds bounds = mesh.bounds;
            float targetZ = bounds.min.z;
            float zTolerance = Mathf.Max(bounds.extents.z * 0.55f, 0.00001f);
            int backSubmeshIndex = mesh.subMeshCount > 1 ? 1 : 0;
            bool[] backSubmeshVertices = BuildBackSubmeshVertexMask(mesh, backSubmeshIndex);

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            float minU = float.PositiveInfinity;
            float maxU = float.NegativeInfinity;
            float minV = float.PositiveInfinity;
            float maxV = float.NegativeInfinity;
            int sampleCount = 0;

            for (int i = 0; i < vertices.Length; i++)
            {
                if (backSubmeshVertices != null && !backSubmeshVertices[i])
                    continue;

                Vector3 vertex = vertices[i];
                if (Mathf.Abs(vertex.z - targetZ) > zTolerance)
                    continue;

                Vector2 uv = uvs[i];
                minX = Mathf.Min(minX, vertex.x);
                maxX = Mathf.Max(maxX, vertex.x);
                minY = Mathf.Min(minY, vertex.y);
                maxY = Mathf.Max(maxY, vertex.y);
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

        static bool[] BuildBackSubmeshVertexMask(Mesh mesh, int submeshIndex)
        {
            if (mesh.subMeshCount <= submeshIndex)
                return null;

            var mask = new bool[mesh.vertexCount];
            int[] indices = mesh.GetTriangles(submeshIndex);
            for (int i = 0; i < indices.Length; i++)
                mask[indices[i]] = true;

            return mask;
        }
    }
}

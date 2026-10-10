using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Combines a normal card's front/back submeshes without changing its geometry.
/// UV2.x selects front (0) or back/edges (1) in the card material. Not for instanced quads.
/// </summary>
public static class CardSinglePassMesh
{
    static readonly Dictionary<Mesh, Mesh> Cache = new Dictionary<Mesh, Mesh>(2);
    static readonly HashSet<Mesh> CombinedMeshes = new HashSet<Mesh>();
    static readonly Dictionary<Mesh, Mesh> SourceMeshes = new Dictionary<Mesh, Mesh>(2);

    // A renderer returning to the two-material path must also recover its two-submesh
    // geometry. Keep that lookup constant-time instead of searching the forward cache.
    public static Mesh GetSourceOrSelf(Mesh mesh)
    {
        if (mesh != null && SourceMeshes.TryGetValue(mesh, out Mesh source))
            return source;
        return mesh;
    }

    public static Mesh Get(Mesh source)
    {
        if (source == null)
            return null;
        // Visual refreshes can pass the generated mesh back to us. Check ownership before
        // rejecting single-submesh inputs; unrelated quads must still use their normal path.
        if (CombinedMeshes.Contains(source))
            return source;
        if (Cache.TryGetValue(source, out Mesh cached) && cached != null)
            return cached;
        if (!source.isReadable || source.subMeshCount < 2)
            return null;

        // This helper is for static card geometry; leave unsupported meshes on their original path.
        if (source.blendShapeCount != 0 || source.HasVertexAttribute(VertexAttribute.BlendWeight))
            return null;
        for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            if (source.GetTopology(submesh) != MeshTopology.Triangles)
                return null;

        int vertexCount = source.vertexCount;
        var sourceIndices = new List<int>(vertexCount);
        var markers = new List<float>(vertexCount);
        for (int i = 0; i < vertexCount; i++)
        {
            sourceIndices.Add(i);
            markers.Add(0f);
        }

        var groups = new byte[vertexCount];
        var backDuplicates = new Dictionary<int, int>();
        var triangles = new List<int>();
        for (int submesh = 0; submesh < source.subMeshCount; submesh++)
        {
            byte group = submesh == 0 ? (byte)1 : (byte)2;
            int[] indices = source.GetTriangles(submesh);
            for (int i = 0; i < indices.Length; i++)
            {
                int sourceIndex = indices[i];
                int destinationIndex = sourceIndex;
                if (groups[sourceIndex] != 0 && groups[sourceIndex] != group)
                {
                    // Rounded cards can share the front ring with edge triangles. A distinct
                    // vertex keeps the texture selector constant across each original triangle.
                    if (!backDuplicates.TryGetValue(sourceIndex, out destinationIndex))
                    {
                        destinationIndex = sourceIndices.Count;
                        backDuplicates.Add(sourceIndex, destinationIndex);
                        sourceIndices.Add(sourceIndex);
                        markers.Add(1f);
                    }
                }
                else
                {
                    groups[sourceIndex] = group;
                    markers[sourceIndex] = group == 1 ? 0f : 1f;
                }
                triangles.Add(destinationIndex);
            }
        }

        Mesh combined = Object.Instantiate(source);
        combined.name = source.name + "_SinglePass";
        combined.hideFlags = HideFlags.HideAndDontSave;
        if (sourceIndices.Count != vertexCount)
        {
            if (sourceIndices.Count > ushort.MaxValue)
                combined.indexFormat = IndexFormat.UInt32;
            combined.vertices = Remap(source.vertices, sourceIndices);
            if (source.HasVertexAttribute(VertexAttribute.Normal))
                combined.normals = Remap(source.normals, sourceIndices);
            if (source.HasVertexAttribute(VertexAttribute.Tangent))
                combined.tangents = Remap(source.tangents, sourceIndices);
            if (source.HasVertexAttribute(VertexAttribute.Color))
                combined.colors = Remap(source.colors, sourceIndices);
        }

        // Preserve every UV channel, including the smooth normals used by outline shaders.
        // UV2.x alone is reserved for the material selector; its other components survive.
        for (int channel = 0; channel < 8; channel++)
        {
            var attribute = (VertexAttribute)((int)VertexAttribute.TexCoord0 + channel);
            bool hasChannel = source.HasVertexAttribute(attribute);
            if (!hasChannel && channel != 1)
                continue;
            if (sourceIndices.Count == vertexCount && channel != 1)
                continue;

            var original = new List<Vector4>();
            if (hasChannel)
                source.GetUVs(channel, original);
            var values = new List<Vector4>(sourceIndices.Count);
            for (int i = 0; i < sourceIndices.Count; i++)
            {
                Vector4 value = hasChannel ? original[sourceIndices[i]] : Vector4.zero;
                if (channel == 1)
                    value.x = markers[i];
                values.Add(value);
            }
            int dimensions = hasChannel ? source.GetVertexAttributeDimension(attribute) : 2;
            SetUVs(combined, channel, values, dimensions);
        }

        combined.subMeshCount = 1;
        combined.SetTriangles(triangles, 0, calculateBounds: false);
        combined.bounds = source.bounds;
        Cache[source] = combined;
        CombinedMeshes.Add(combined);
        SourceMeshes[combined] = source;
        return combined;
    }

    static T[] Remap<T>(T[] values, List<int> sourceIndices)
    {
        var result = new T[sourceIndices.Count];
        for (int i = 0; i < sourceIndices.Count; i++)
            result[i] = values[sourceIndices[i]];
        return result;
    }

    static void SetUVs(Mesh mesh, int channel, List<Vector4> values, int dimensions)
    {
        if (dimensions <= 2)
        {
            var uv = new List<Vector2>(values.Count);
            for (int i = 0; i < values.Count; i++)
                uv.Add(new Vector2(values[i].x, values[i].y));
            mesh.SetUVs(channel, uv);
        }
        else if (dimensions == 3)
        {
            var uv = new List<Vector3>(values.Count);
            for (int i = 0; i < values.Count; i++)
                uv.Add(new Vector3(values[i].x, values[i].y, values[i].z));
            mesh.SetUVs(channel, uv);
        }
        else
        {
            mesh.SetUVs(channel, values);
        }
    }
}

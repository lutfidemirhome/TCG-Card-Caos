using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Bakes separate outline geometry without changing model import settings or source meshes.</summary>
public static class SkillCabinetOutlineBaker
{
    const string ShelfModel = "Assets/ModernSupermarket/Models/Furniture/Shelf_kicg9f/Shelf_kicg9f.fbx";
    const long ShelfMeshId = -5176549954201968157;
    const string CounterModel = "Assets/ModernSupermarket/Models/Furniture/Counter_attlsv/Counter_attlsv.fbx";
    const long CounterMeshId = 5671296814648227334;
    const string ShelfOutput = "Assets/Resources/UI/Skills/CabinetOutline.asset";
    const string CounterOutput = "Assets/Resources/UI/Skills/PsaCabinetOutline.asset";

    [MenuItem("TCG Card Chaos/Skills/Bake Cabinet Exterior Outlines")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Bake cabinet outlines outside Play Mode.");
        Mesh shelf = null, counter = null;
        try
        {
            // Match the exact subassets referenced by the exterior renderers. The shelf
            // model also contains an internal board mesh, which must never be substituted.
            shelf = Build(FindMesh(ShelfModel, ShelfMeshId));
            counter = Build(FindMesh(CounterModel, CounterMeshId));
            Store(shelf, ShelfOutput);
            Store(counter, CounterOutput);
            Debug.Log("[Skills] Cabinet exterior outline meshes baked with smooth UV3 normals; original meshes and importer settings unchanged.");
        }
        finally
        {
            if (shelf != null && !AssetDatabase.Contains(shelf)) UnityEngine.Object.DestroyImmediate(shelf);
            if (counter != null && !AssetDatabase.Contains(counter)) UnityEngine.Object.DestroyImmediate(counter);
        }
    }

    static Mesh FindMesh(string modelPath, long localId)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            if (asset is Mesh mesh && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string _, out long id)
                && id == localId) return mesh;
        throw new InvalidOperationException("Cabinet exterior mesh subasset not found: " + modelPath + " / " + localId);
    }

    static Mesh Build(Mesh source)
    {
        Mesh result = new Mesh { name = source.name, indexFormat = source.indexFormat };
        try
        {
            // UnityEditor.MeshUtility can inspect importer-owned meshes whose runtime
            // Read/Write flag is off. This is a read-only snapshot, not a reimport.
            using (Mesh.MeshDataArray snapshot = MeshUtility.AcquireReadOnlyMeshData(source))
            {
                Mesh.MeshData data = snapshot[0];
                using (var positions = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                {
                    data.GetVertices(positions);
                    result.SetVertices(positions);
                }
                if (data.HasVertexAttribute(VertexAttribute.Normal))
                    using (var normals = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                    {
                        data.GetNormals(normals);
                        result.SetNormals(normals);
                    }

                var triangles = new List<int>();
                for (int submesh = 0; submesh < data.subMeshCount; submesh++)
                {
                    SubMeshDescriptor descriptor = data.GetSubMesh(submesh);
                    if (descriptor.topology != MeshTopology.Triangles)
                        throw new InvalidOperationException("Cabinet exterior has unsupported non-triangle geometry: " + source.name);
                    using (var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp))
                    {
                        data.GetIndices(indices, submesh, applyBaseVertex: true);
                        for (int i = 0; i < indices.Length; i++) triangles.Add(indices[i]);
                    }
                }
                result.SetTriangles(triangles, 0);
            }
            if (result.normals.Length != result.vertexCount) result.RecalculateNormals();
            SkillMarker.SetSmoothOutlineNormals(result);
            result.bounds = source.bounds;
            return result;
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(result);
            throw;
        }
    }

    static void Store(Mesh generated, string path)
    {
        // Unity requires a main asset's object name to match its filename.
        generated.name = System.IO.Path.GetFileNameWithoutExtension(path);
        UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
        if (existing != null)
        {
            if (!(existing is Mesh mesh)) throw new InvalidOperationException("Outline output is not a mesh: " + path);
            // Preserve the existing .meta/GUID and references on repeated explicit bakes.
            EditorUtility.CopySerialized(generated, mesh);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
        }
        else
        {
            AssetDatabase.CreateAsset(generated, path);
        }
    }
}

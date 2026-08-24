using UnityEditor;
using UnityEngine;

/// <summary>
/// Stairs FBX is already Y-up. Bake it to 2.5 m with feet at the origin.
/// Do not stand this mesh on end — that bake is only for the old vertical ladder.
/// </summary>
public class LadderMeshPostprocessor : AssetPostprocessor
{
    const string StairsPath = "Assets/Art/Props/Ladder/Stairs.fbx";
    const string LegacyLadderPath = "Assets/Art/Props/Ladder/Ladder.fbx";
    const float TargetHeight = 2.5f;

    void OnPostprocessModel(GameObject root)
    {
        string path = assetPath.Replace('\\', '/');
        if (path == StairsPath)
        {
            ResetLocalTransforms(root.transform);
            BakeMeshes(root, BakeStaircase);
            return;
        }

        if (path == LegacyLadderPath)
        {
            ResetLocalTransforms(root.transform);
            BakeMeshes(root, BakeUpright);
        }
    }

    static void BakeMeshes(GameObject root, System.Action<Mesh> bake)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].sharedMesh != null)
                bake(filters[i].sharedMesh);
        }
    }

    static void ResetLocalTransforms(Transform transform)
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        for (int i = 0; i < transform.childCount; i++)
            ResetLocalTransforms(transform.GetChild(i));
    }

    static void BakeStaircase(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        if (vertices == null || vertices.Length == 0)
            return;

        Bounds bounds = BoundsOf(vertices);
        float height = bounds.size.y;
        float scale = height > 0.0001f ? TargetHeight / height : 1f;
        Vector3 shift = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.min.z);
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = (vertices[i] + shift) * scale;

        mesh.vertices = vertices;
        mesh.RecalculateBounds();
    }

    static void BakeUpright(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        if (vertices == null || vertices.Length == 0)
            return;

        Bounds bounds = BoundsOf(vertices);
        Quaternion rotation = RotationToY(bounds.size);
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = rotation * vertices[i];

        bounds = BoundsOf(vertices);
        float height = bounds.size.y;
        float scale = height > 0.0001f ? TargetHeight / height : 1f;
        Vector3 shift = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = vertices[i] * scale + shift * scale;

        mesh.vertices = vertices;

        Vector3[] normals = mesh.normals;
        if (normals != null && normals.Length == vertices.Length)
        {
            for (int i = 0; i < normals.Length; i++)
                normals[i] = rotation * normals[i];
            mesh.normals = normals;
        }

        Vector4[] tangents = mesh.tangents;
        if (tangents != null && tangents.Length == vertices.Length)
        {
            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 tangent = rotation * new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
            }

            mesh.tangents = tangents;
        }

        mesh.RecalculateBounds();
    }

    static Quaternion RotationToY(Vector3 size)
    {
        if (size.z >= size.x && size.z > size.y)
            return Quaternion.Euler(-90f, 0f, 0f);
        if (size.x >= size.z && size.x > size.y)
            return Quaternion.Euler(0f, 0f, 90f);
        return Quaternion.identity;
    }

    static Bounds BoundsOf(Vector3[] vertices)
    {
        var bounds = new Bounds(vertices[0], Vector3.zero);
        for (int i = 1; i < vertices.Length; i++)
            bounds.Encapsulate(vertices[i]);
        return bounds;
    }
}

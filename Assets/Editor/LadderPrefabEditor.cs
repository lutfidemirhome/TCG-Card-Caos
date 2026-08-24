using System.IO;
using UnityEditor;
using UnityEngine;

public static class LadderPrefabEditor
{
    const string ModelPath = "Assets/Art/Props/Ladder/Ladder.fbx";
    const string MaterialPath = "Assets/Art/Props/Ladder/Materials/Ladder.mat";
    const string AlbedoPath = "Assets/Art/Props/Ladder/Textures/LadderULayout.png";
    const string NormalPath = "Assets/Art/Props/Ladder/Textures/NormalMap.png";
    const string OcclusionPath = "Assets/Art/Props/Ladder/Textures/AO.png";
    const string PrefabPath = "Assets/Prefabs/Props/Ladder.prefab";

    [MenuItem("TCG Card Caos/Create Ladder Prefab")]
    public static void CreateOrUpdatePrefab()
    {
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelPrefab == null)
        {
            Debug.LogError("Ladder model not found at " + ModelPath);
            return;
        }

        Material material = EnsureMaterial();
        var root = new GameObject("Ladder");
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, root.transform);
        visual.name = "Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        ResetLocalTransforms(visual.transform);
        ApplyMaterialAndColliders(visual, material);

        EnsureFolder("Assets/Prefabs/Props");
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = prefabAsset;
        EditorGUIUtility.PingObject(prefabAsset);
        Debug.Log("TCG Card Caos: Ladder prefab ready at " + PrefabPath);
    }

    static void ResetLocalTransforms(Transform transform)
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        for (int i = 0; i < transform.childCount; i++)
            ResetLocalTransforms(transform.GetChild(i));
    }

    static Material EnsureMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        Texture albedo = AssetDatabase.LoadAssetAtPath<Texture>(AlbedoPath);
        Texture normal = AssetDatabase.LoadAssetAtPath<Texture>(NormalPath);
        Texture occlusion = AssetDatabase.LoadAssetAtPath<Texture>(OcclusionPath);

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", albedo);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", albedo);
        if (material.HasProperty("_BumpMap"))
            material.SetTexture("_BumpMap", normal);
        if (material.HasProperty("_OcclusionMap"))
            material.SetTexture("_OcclusionMap", occlusion);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.12f);
        if (material.HasProperty("_OcclusionStrength"))
            material.SetFloat("_OcclusionStrength", 1f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", 0f);
        if (material.HasProperty("_EnvironmentReflections"))
            material.SetFloat("_EnvironmentReflections", 0f);
        if (material.HasProperty("_SpecularHighlights"))
            material.SetFloat("_SpecularHighlights", 0f);

        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_OCCLUSIONMAP");
        material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        material.doubleSidedGI = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void ApplyMaterialAndColliders(GameObject visual, Material material)
    {
        MeshRenderer[] renderers = visual.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            var slots = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
            for (int s = 0; s < slots.Length; s++)
                slots[s] = material;
            renderer.sharedMaterials = slots;
        }

        MeshFilter[] filters = visual.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            if (filter.sharedMesh == null)
                continue;

            MeshCollider collider = filter.GetComponent<MeshCollider>();
            if (collider == null)
                collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }
}

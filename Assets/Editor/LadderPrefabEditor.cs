using UnityEditor;
using UnityEngine;

public static class LadderPrefabEditor
{
    const string ModelPath = "Assets/Art/Props/Ladder/Stairs.fbx";
    const string MaterialPath = "Assets/Art/Props/Ladder/Materials/Ladder.mat";
    const string RailMaterialPath = "Assets/Art/Props/Ladder/Materials/StairsRail.mat";
    const string AlbedoPath = "Assets/Art/Props/Ladder/Textures/STAIRCASE.png";
    const string NormalPath = "Assets/Art/Props/Ladder/Textures/Staircase_n.png";
    const string PrefabPath = "Assets/Prefabs/Props/Ladder.prefab";

    [MenuItem("TCG Card Caos/Create Ladder Prefab")]
    public static void CreateOrUpdatePrefab()
    {
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
        Mesh stairsMesh = LoadFirstMesh(ModelPath);
        if (stairsMesh == null)
        {
            Debug.LogError("Stairs mesh not found at " + ModelPath);
            return;
        }

        Material material = EnsureMaterial();
        Material railMaterial = EnsureRailMaterial();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform visual = root.transform.Find("Visual");
            if (visual == null)
                visual = root.transform;

            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;

            MeshFilter filter = visual.GetComponent<MeshFilter>();
            if (filter == null)
                filter = visual.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = stairsMesh;

            MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
            if (renderer == null)
                renderer = visual.gameObject.AddComponent<MeshRenderer>();
            int slotCount = Mathf.Max(2, stairsMesh.subMeshCount);
            var slots = new Material[slotCount];
            for (int i = 0; i < slotCount; i++)
                slots[i] = i == 1 ? railMaterial : material;
            renderer.sharedMaterials = slots;

            MeshCollider collider = visual.GetComponent<MeshCollider>();
            if (collider == null)
                collider = visual.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = stairsMesh;
            collider.convex = false;
            collider.enabled = true;

            BoxCollider trigger = root.GetComponent<BoxCollider>();
            if (trigger != null)
                Object.DestroyImmediate(trigger);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("TCG Card Caos: Ladder prefab now uses the stairs model at " + PrefabPath);
    }

    static Mesh LoadFirstMesh(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        Mesh best = null;
        int bestVerts = -1;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Mesh mesh && mesh.vertexCount > bestVerts)
            {
                best = mesh;
                bestVerts = mesh.vertexCount;
            }
        }

        return best;
    }

    static Material EnsureMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Toony Colors Pro 2/Hybrid Shader 2")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        Texture albedo = AssetDatabase.LoadAssetAtPath<Texture>(AlbedoPath);
        Texture normal = AssetDatabase.LoadAssetAtPath<Texture>(NormalPath);

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", albedo);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", albedo);
        if (material.HasProperty("_BumpMap"))
            material.SetTexture("_BumpMap", normal);
        if (material.HasProperty("_OcclusionMap"))
            material.SetTexture("_OcclusionMap", null);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.08f);
        if (material.HasProperty("_UseNormalMap"))
            material.SetFloat("_UseNormalMap", 1f);
        if (material.HasProperty("_UseOcclusion"))
            material.SetFloat("_UseOcclusion", 0f);
        if (material.HasProperty("_UseSpecular"))
            material.SetFloat("_UseSpecular", 0f);
        if (material.HasProperty("_UseReflections"))
            material.SetFloat("_UseReflections", 0f);
        if (material.HasProperty("_UseMatCap"))
            material.SetFloat("_UseMatCap", 0f);
        if (material.HasProperty("_SpecularHighlights"))
            material.SetFloat("_SpecularHighlights", 0f);
        if (material.HasProperty("_EnvironmentReflections"))
            material.SetFloat("_EnvironmentReflections", 0f);

        material.EnableKeyword("_NORMALMAP");
        material.DisableKeyword("_OCCLUSIONMAP");
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material EnsureRailMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(RailMaterialPath);
        Shader shader = Shader.Find("Toony Colors Pro 2/Hybrid Shader 2")
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, RailMaterialPath);
        }

        var railColor = new Color(0.42f, 0.42f, 0.45f, 1f);
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", null);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", null);
        if (material.HasProperty("_BumpMap"))
            material.SetTexture("_BumpMap", null);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", railColor);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", railColor);
        if (material.HasProperty("_UseNormalMap"))
            material.SetFloat("_UseNormalMap", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", 0f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.35f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.22f);

        material.DisableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(material);
        return material;
    }
}

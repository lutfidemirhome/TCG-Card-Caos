#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class MaterialFixEditor
{
    const string PipelinePath = "Assets/Settings/URP_Pipeline.asset";
    const string GroundMaterialPath = "Assets/Art/Materials/Ground.mat";
    const string ObjectMaterialPath = "Assets/Art/Materials/TestObject.mat";

    [MenuItem("TCG Card Caos/Fix Pink Materials")]
    public static void FixPinkMaterials()
    {
        EnsureFolder("Assets/Art");
        EnsureFolder("Assets/Art/Materials");

        AssignPipelineToAllQualityLevels();

        Material groundMaterial = GetOrCreateLitMaterial(GroundMaterialPath, new Color(0.35f, 0.32f, 0.28f));
        Material objectMaterial = GetOrCreateLitMaterial(ObjectMaterialPath, new Color(0.25f, 0.55f, 0.95f));

        MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer.gameObject.name == "Ground")
                renderer.sharedMaterial = groundMaterial;
            else if (renderer.gameObject.name == "TestInteractable")
                renderer.sharedMaterial = objectMaterial;
            else if (NeedsUrpMaterial(renderer.sharedMaterial))
                renderer.sharedMaterial = groundMaterial;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log("Pink materials fixed. Ground should look normal now.");
    }

    public static Material GetOrCreateLitMaterial(string path, Color color)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            ApplyBaseColor(existing, color);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader);
        ApplyBaseColor(material, color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void AssignPipelineToAllQualityLevels()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
            return;

        GraphicsSettings.defaultRenderPipeline = pipeline;

        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipeline;
        }

        QualitySettings.SetQualityLevel(current, false);
    }

    static bool NeedsUrpMaterial(Material material)
    {
        if (material == null || material.shader == null)
            return true;

        string shaderName = material.shader.name;
        return shaderName == "Hidden/InternalErrorShader"
            || shaderName == "Standard"
            || shaderName.StartsWith("Legacy Shaders/");
    }

    static void ApplyBaseColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        material.color = color;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folderName = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
static class EnsureURPAssets
{
    const string PipelinePath = "Assets/Settings/URP_Pipeline.asset";
    const string RendererPath = "Assets/Settings/URP_ForwardRenderer.asset";

    static EnsureURPAssets()
    {
        EditorApplication.delayCall += TryCreateMissingPipeline;
    }

    static void TryCreateMissingPipeline()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var current = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (current != null && AssetDatabase.Contains(current))
            return;

        EnsureFolder("Assets/Settings");

        var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(renderer, RendererPath);

        var pipeline = UniversalRenderPipelineAsset.Create(renderer);
        AssetDatabase.CreateAsset(pipeline, PipelinePath);
        AssetDatabase.SaveAssets();

        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;

        Debug.Log("TCG Card Chaos: Created missing URP pipeline assets.");
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

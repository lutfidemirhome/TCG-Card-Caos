using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps Resources card textures in sync when template PNGs under Assets/Art/Cards change.
/// Card fronts stream their existing mipmaps rather than keeping thousands of full-size faces resident.
/// </summary>
public class CardArtTexturePostprocessor : AssetPostprocessor
{
    static bool _refreshScheduled;

    void OnPreprocessTexture()
    {
        const string frontsRoot = "Assets/Art/Cards/";
        string path = assetPath.Replace('\\', '/');
        // Shared templates serve other paths; only individual fronts in category
        // subfolders belong to this streaming policy.
        if (!path.StartsWith(frontsRoot, System.StringComparison.Ordinal)
            || path.IndexOf('/', frontsRoot.Length) < 0)
            return;

        var importer = (TextureImporter)assetImporter;
        if (importer.textureType == TextureImporterType.Default && importer.mipmapEnabled)
            importer.streamingMipmaps = true;
    }

    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (!ShouldRefresh(importedAssets)
            && !ShouldRefresh(movedAssets)
            && !ShouldRefresh(movedFromAssetPaths))
        {
            return;
        }

        ScheduleRefresh();
    }

    static bool ShouldRefresh(string[] paths)
    {
        if (paths == null)
            return false;

        for (int i = 0; i < paths.Length; i++)
        {
            string normalized = paths[i]?.Replace('\\', '/');
            if (normalized == CardArtLibrary.FrontTextureAssetPath
                || normalized == CardArtLibrary.BackTextureAssetPath)
            {
                return true;
            }
        }

        return false;
    }

    static void ScheduleRefresh()
    {
        if (_refreshScheduled)
            return;

        _refreshScheduled = true;
        EditorApplication.delayCall += () =>
        {
            _refreshScheduled = false;
            CardArtSetup.RefreshBakedTextures();
        };
    }
}

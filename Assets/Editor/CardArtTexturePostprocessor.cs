using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps Resources card textures in sync when template PNGs under Assets/Art/Cards change.
/// </summary>
public class CardArtTexturePostprocessor : AssetPostprocessor
{
    static bool _refreshScheduled;

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

using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Library/PlayerDataCache is a leftover cooked-player copy after a standalone build
/// (~4 GB on this project). It is not part of the game; the next build recreates it.
/// </summary>
public static class PlayerDataCacheCleaner
{
    const string MenuPath = "TCG Card Chaos/Clear Player Data Cache";

    [MenuItem(MenuPath)]
    public static void ClearFromMenu()
    {
        string path = CachePath();
        if (!Directory.Exists(path))
        {
            EditorUtility.DisplayDialog("Player Data Cache", "Zaten yok. Diskte ekstra kopya yok.", "OK");
            return;
        }

        string size = FormatSize(DirectorySizeBytes(path));
        if (!EditorUtility.DisplayDialog(
                "Player Data Cache",
                size + " silinecek (Library/PlayerDataCache).\n"
                + "Oyunu bozmaz. Sonraki build yeniden üretir, o build biraz yavaş olabilir.",
                "Sil",
                "İptal"))
            return;

        if (TryDelete(path, out string error))
            EditorUtility.DisplayDialog("Player Data Cache", size + " silindi.", "OK");
        else
            EditorUtility.DisplayDialog("Player Data Cache", "Silinemedi: " + error, "OK");
    }

    public static void ClearQuiet()
    {
        string path = CachePath();
        if (!Directory.Exists(path))
            return;

        if (TryDelete(path, out string error))
            Debug.Log("TCG Card Chaos: PlayerDataCache cleared (" + path + ").");
        else
            Debug.LogWarning("TCG Card Chaos: PlayerDataCache not cleared: " + error);
    }

    static string CachePath()
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "PlayerDataCache");
    }

    static bool TryDelete(string path, out string error)
    {
        error = null;
        try
        {
            if (!FileUtil.DeleteFileOrDirectory(path))
                Directory.Delete(path, true);
            return true;
        }
        catch (IOException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    static long DirectorySizeBytes(string path)
    {
        long total = 0;
        var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
            total += new FileInfo(files[i]).Length;
        return total;
    }

    static string FormatSize(long bytes)
    {
        if (bytes >= 1073741824)
            return (bytes / 1073741824f).ToString("0.0") + " GB";
        if (bytes >= 1048576)
            return (bytes / 1048576f).ToString("0") + " MB";
        return bytes + " B";
    }
}

class ClearPlayerDataCacheOnBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 1000;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows64
            && report.summary.platform != BuildTarget.StandaloneOSX)
            return;

        EditorApplication.delayCall += PlayerDataCacheCleaner.ClearQuiet;
    }
}

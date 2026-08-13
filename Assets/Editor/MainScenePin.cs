#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class MainScenePin
{
    const string MainSceneGuid = "a09345514c60f4aeca8296e9eef78562";
    const string ScenesFolderGuid = "69f010b889fe4451194bbccd802b5362";
    const string ShortcutPath = "Assets/Scenes/MainScene Shortcut.asset";
    const string FavoritesPath = "UserSettings/FavoriteAssets.json";

    static readonly string[] PinGuids = { MainSceneGuid, ScenesFolderGuid };

    static MainScenePin()
    {
        EditorApplication.delayCall += EnsurePinned;
    }

    static void EnsurePinned()
    {
        EnsureShortcutAsset();
        EnsureFavoriteAssetsJson();
    }

    static void EnsureShortcutAsset()
    {
        if (AssetDatabase.LoadAssetAtPath<MainSceneShortcut>(ShortcutPath) != null)
            return;

        var shortcut = ScriptableObject.CreateInstance<MainSceneShortcut>();
        AssetDatabase.CreateAsset(shortcut, ShortcutPath);
        AssetDatabase.SaveAssets();
    }

    static void EnsureFavoriteAssetsJson()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string favoritesFile = Path.Combine(projectRoot, FavoritesPath);
        Directory.CreateDirectory(Path.GetDirectoryName(favoritesFile));

        var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        TryReadExistingGuids(favoritesFile, guids);

        bool changed = false;
        for (int i = 0; i < PinGuids.Length; i++)
        {
            if (guids.Add(PinGuids[i]))
                changed = true;
        }

        string shortcutGuid = AssetDatabase.AssetPathToGUID(ShortcutPath);
        if (!string.IsNullOrEmpty(shortcutGuid) && guids.Add(shortcutGuid))
            changed = true;

        if (!changed && File.Exists(favoritesFile))
            return;

        var guidList = new List<string>(guids);
        guidList.Sort(StringComparer.OrdinalIgnoreCase);

        string json = JsonUtility.ToJson(new FavoriteAssetsData { Guids = guidList }, true);
        File.WriteAllText(favoritesFile, json);
    }

    static void TryReadExistingGuids(string favoritesFile, HashSet<string> guids)
    {
        if (!File.Exists(favoritesFile))
            return;

        try
        {
            string json = File.ReadAllText(favoritesFile);
            FavoriteAssetsData data = JsonUtility.FromJson<FavoriteAssetsData>(json);
            if (data?.Guids == null)
                return;

            for (int i = 0; i < data.Guids.Count; i++)
            {
                if (!string.IsNullOrEmpty(data.Guids[i]))
                    guids.Add(data.Guids[i]);
            }
        }
        catch
        {
            // Keep existing favorites file if format differs.
        }
    }

    [Serializable]
    class FavoriteAssetsData
    {
        public List<string> Guids = new List<string>();
    }
}
#endif

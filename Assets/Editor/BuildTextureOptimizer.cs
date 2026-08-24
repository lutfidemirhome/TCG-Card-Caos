using UnityEditor;
using UnityEngine;

/// <summary>
/// Caps imported texture size for the heavy folders that ship in the player:
/// card faces (Resources definitions reference Assets/Art/Cards), environment, booster packs.
/// Matches <see cref="CardTextureSettings.DetailMaxSize"/> so inspect quality stays as designed.
/// </summary>
public class BuildTextureOptimizer : AssetPostprocessor
{
    const int CardMaxSize = CardTextureSettings.DetailMaxSize;
    const int EnvironmentMaxSize = 1024;

    [MenuItem("TCG Card Caos/Optimize Build Textures")]
    public static void OptimizeFromMenu()
    {
        int changed = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            changed += ApplyToFolder("Assets/Art/Cards", CardMaxSize);
            changed += ApplyToFolder("Assets/Resources/Cards", CardMaxSize);
            changed += ApplyToFolder("Assets/AE_New_York/Textures", EnvironmentMaxSize);
            changed += ApplyToFolder("Assets/ModernSupermarket/Textures", EnvironmentMaxSize);
            changed += ApplyToFolder("Assets/ModernSupermarket/Models", EnvironmentMaxSize);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Optimize Build Textures",
            changed + " texture importer(s) capped.\n\n"
            + "Cards: "
            + CardMaxSize
            + "px (inspect size). Environment: "
            + EnvironmentMaxSize
            + "px.\nWait for Unity to finish reimporting, then build again.",
            "OK");
    }

    static int ApplyToFolder(string folder, int maxSize)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return 0;

        string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { folder });
        int changed = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || !NeedsCap(importer, maxSize))
                continue;

            CapImporter(importer, maxSize);
            importer.SaveAndReimport();
            changed++;
        }

        return changed;
    }

    void OnPreprocessTexture()
    {
        int maxSize = MaxSizeForPath(assetPath);
        if (maxSize <= 0)
            return;

        CapImporter((TextureImporter)assetImporter, maxSize);
    }

    static int MaxSizeForPath(string assetPath)
    {
        string path = assetPath.Replace('\\', '/');
        if (path.StartsWith("Assets/Art/Cards/") || path.StartsWith("Assets/Resources/Cards/"))
            return CardMaxSize;
        if (path.StartsWith("Assets/AE_New_York/Textures/"))
            return EnvironmentMaxSize;
        if (path.StartsWith("Assets/ModernSupermarket/Textures/")
            || path.StartsWith("Assets/ModernSupermarket/Models/"))
            return EnvironmentMaxSize;
        return 0;
    }

    static bool NeedsCap(TextureImporter importer, int maxSize)
    {
        if (importer.maxTextureSize > maxSize)
            return true;

        TextureImporterPlatformSettings defaults = importer.GetDefaultPlatformTextureSettings();
        if (defaults.maxTextureSize > maxSize)
            return true;

        TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
        return standalone.maxTextureSize > maxSize;
    }

    static void CapImporter(TextureImporter importer, int maxSize)
    {
        if (importer.maxTextureSize > maxSize)
            importer.maxTextureSize = maxSize;

        CapPlatform(importer, importer.GetDefaultPlatformTextureSettings(), maxSize, overridePlatform: false);
        CapPlatform(importer, importer.GetPlatformTextureSettings("Standalone"), maxSize, overridePlatform: true);
    }

    static void CapPlatform(
        TextureImporter importer,
        TextureImporterPlatformSettings settings,
        int maxSize,
        bool overridePlatform)
    {
        if (settings.maxTextureSize > maxSize)
            settings.maxTextureSize = maxSize;

        settings.textureCompression = TextureImporterCompression.Compressed;
        settings.crunchedCompression = false;
        if (overridePlatform)
            settings.overridden = true;

        importer.SetPlatformTextureSettings(settings);
    }
}

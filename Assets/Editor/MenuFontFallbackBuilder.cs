using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Builds the TMP fallback chain for the writing systems Baloo 2 does not ship: Cyrillic (ru) and
/// CJK (zh-Hans, zh-Hant, ja, ko). Drop the source fonts into <see cref="FallbackFolder"/> and run
/// the menu command; each font becomes a Dynamic SDF asset so only the glyphs actually used by the
/// translations get rasterized.
/// Menu: TCG Card Caos → UI → Build Font Fallbacks
/// </summary>
public static class MenuFontFallbackBuilder
{
    const string FallbackFolder = "Assets/TextMesh Pro/Fonts/Fallback";
    const string FontsFolder = "Assets/TextMesh Pro/Fonts";
    const string PrimaryFontAssetPath = "Assets/TextMesh Pro/Fonts/Baloo2-ExtraBold SDF.asset";

    // Matches the primary font so SDF sharpness stays consistent across the fallback chain.
    const int SamplingPointSize = 68;
    const int AtlasPadding = 6;
    const int AtlasSize = 1024;

    /// <summary>Fallback slots in resolution order; TMP walks this list for every missing glyph.</summary>
    static readonly (string Label, string[] Keywords, string Sample)[] Slots =
    {
        ("Cyrillic (ru)", new[] { "cyrillic", "notosans-" }, "Русский Продолжить Настройки"),
        ("Chinese Simplified (zh-Hans)", new[] { "sc", "simplifiedchinese", "hans" }, "简体中文 继续 设置"),
        ("Chinese Traditional (zh-Hant)", new[] { "tc", "traditionalchinese", "hant" }, "繁體中文 繼續 設定"),
        ("Japanese (ja)", new[] { "jp", "japanese" }, "日本語 続ける 設定"),
        ("Korean (ko)", new[] { "kr", "korean" }, "한국어 계속하기 설정"),
    };

    [MenuItem("TCG Card Caos/UI/Build Font Fallbacks")]
    public static void BuildFontFallbacks()
    {
        var primary = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PrimaryFontAssetPath);
        if (primary == null)
        {
            Debug.LogError("[MenuFontFallbackBuilder] Primary font asset not found at " + PrimaryFontAssetPath);
            return;
        }

        if (!Directory.Exists(FallbackFolder))
        {
            Directory.CreateDirectory(FallbackFolder);
            AssetDatabase.Refresh();
            Debug.LogWarning(
                "[MenuFontFallbackBuilder] Created " + FallbackFolder + ".\n"
                + "Drop the fallback fonts in there and run this command again:\n"
                + DescribeExpectedFonts());
            return;
        }

        List<string> sourcePaths = FindSourceFonts();
        if (sourcePaths.Count == 0)
        {
            Debug.LogWarning(
                "[MenuFontFallbackBuilder] No Noto fallback .ttf/.otf files found.\n"
                + "Put these in " + FallbackFolder + ":\n"
                + DescribeExpectedFonts());
            return;
        }

        if (!HasAnyFileInFolder(FallbackFolder))
        {
            Debug.Log(
                "[MenuFontFallbackBuilder] Using Noto fonts from " + FontsFolder
                + " (recommended folder: " + FallbackFolder + ").");
        }

        FontEngine.InitializeFontEngine();

        var fallbacks = new List<TMP_FontAsset>();
        var report = new List<string>();

        for (int i = 0; i < Slots.Length; i++)
        {
            (string label, string[] keywords, string sample) = Slots[i];

            string sourcePath = MatchSourceFont(sourcePaths, keywords);
            if (sourcePath == null)
            {
                report.Add("  [missing] " + label);
                continue;
            }

            TMP_FontAsset fallback = GetOrCreateFallbackAsset(sourcePath);
            if (fallback == null)
            {
                report.Add("  [failed]  " + label + "  <- " + Path.GetFileName(sourcePath));
                continue;
            }

            // One font may cover several slots (e.g. a combined CJK face); keep the chain deduped.
            if (!fallbacks.Contains(fallback))
                fallbacks.Add(fallback);

            report.Add("  [ok]      " + label + "  <- " + Path.GetFileName(sourcePath)
                       + "  " + DescribeCoverage(fallback, sample));
            EditorUtility.SetDirty(fallback);
        }

        primary.fallbackFontAssetTable = fallbacks;
        EditorUtility.SetDirty(primary);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MenuFontFallbackBuilder] Fallback chain for " + primary.name + " ("
            + fallbacks.Count + "/" + Slots.Length + " slots):\n"
            + string.Join("\n", report));
    }

    static List<string> FindSourceFonts()
    {
        var paths = new List<string>();
        CollectFontsInFolder(FallbackFolder, paths);

        if (paths.Count == 0)
            CollectFontsInFolder(FontsFolder, paths, excludeFallbackFolder: true);

        return paths;
    }

    static bool HasAnyFileInFolder(string folder)
    {
        if (!Directory.Exists(folder))
            return false;

        return Directory.GetFiles(folder, "*.ttf").Length > 0
               || Directory.GetFiles(folder, "*.otf").Length > 0;
    }

    static void CollectFontsInFolder(string folder, List<string> paths, bool excludeFallbackFolder = false)
    {
        if (!Directory.Exists(folder))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Font", new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (excludeFallbackFolder && path.StartsWith(FallbackFolder + "/"))
                continue;

            string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (!fileName.StartsWith("notosans"))
                continue;

            if (!paths.Contains(path))
                paths.Add(path);
        }
    }

    static string MatchSourceFont(List<string> sourcePaths, string[] keywords)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            for (int j = 0; j < sourcePaths.Count; j++)
            {
                string fileName = Path.GetFileNameWithoutExtension(sourcePaths[j]).ToLowerInvariant();
                if (MatchesKeyword(fileName, keywords[i]))
                    return sourcePaths[j];
            }
        }

        return null;
    }

    /// <summary>
    /// Region codes live between "NotoSans" and the weight suffix, so match on that boundary to keep
    /// "NotoSansKR-Bold" out of the Cyrillic slot and "NotoSansSC" out of the Traditional slot.
    /// </summary>
    static bool MatchesKeyword(string fileName, string keyword)
    {
        if (keyword.Length > 2)
            return fileName.Contains(keyword);

        int index = fileName.IndexOf(keyword, System.StringComparison.Ordinal);
        while (index >= 0)
        {
            bool endsRegion = index + keyword.Length == fileName.Length
                              || !char.IsLetter(fileName[index + keyword.Length]);
            if (endsRegion)
                return true;

            index = fileName.IndexOf(keyword, index + 1, System.StringComparison.Ordinal);
        }

        return false;
    }

    static TMP_FontAsset GetOrCreateFallbackAsset(string sourcePath)
    {
        string assetPath = Path.ChangeExtension(sourcePath, null) + " SDF.asset";

        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null)
            return existing;

        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (sourceFont == null)
            return null;

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
            AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);

        if (fontAsset == null)
        {
            Debug.LogError(
                "[MenuFontFallbackBuilder] Could not load font face for " + sourcePath
                + ". Enable \"Include Font Data\" in its import settings.", sourceFont);
            return null;
        }

        AssetDatabase.CreateAsset(fontAsset, assetPath);

        // Atlas texture and material must become sub-assets or they are lost on reload.
        Texture2D atlas = fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0
            ? fontAsset.atlasTextures[0]
            : null;

        if (atlas != null)
            AssetDatabase.AddObjectToAsset(atlas, fontAsset);

        if (fontAsset.material != null)
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

        AssetDatabase.SaveAssets();
        return fontAsset;
    }

    static string DescribeCoverage(TMP_FontAsset fontAsset, string sample)
    {
        fontAsset.TryAddCharacters(sample, out string missingCharacters);

        return string.IsNullOrEmpty(missingCharacters)
            ? "(sample renders)"
            : "(sample missing: " + missingCharacters + ")";
    }

    static string DescribeExpectedFonts() =>
        "  NotoSans-Bold.ttf     -> Cyrillic (ru)\n"
        + "  NotoSansSC-Bold.ttf   -> Chinese Simplified\n"
        + "  NotoSansTC-Bold.ttf   -> Chinese Traditional\n"
        + "  NotoSansJP-Bold.ttf   -> Japanese\n"
        + "  NotoSansKR-Bold.ttf   -> Korean";
}

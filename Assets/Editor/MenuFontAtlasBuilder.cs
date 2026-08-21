using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds the Baloo 2 SDF atlas so Turkish and other Latin-Extended glyphs render in the menu font
/// instead of falling back to LiberationSans (which looks noticeably thinner next to ExtraBold).
/// The asset is switched to Dynamic population so any glyph missing at runtime is added on demand.
/// Menu: TCG Card Caos → UI → Rebuild Menu Font Atlas
/// </summary>
public static class MenuFontAtlasBuilder
{
    const string FontAssetPath = "Assets/TextMesh Pro/Fonts/Baloo2-ExtraBold SDF.asset";
    const string SourceFontPath = "Assets/TextMesh Pro/Fonts/Baloo2-ExtraBold.ttf";
    const int AtlasSize = 1024;

    [MenuItem("TCG Card Caos/UI/Rebuild Menu Font Atlas")]
    public static void RebuildMenuFontAtlas()
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset == null)
        {
            Debug.LogError("[MenuFontAtlasBuilder] Font asset not found at " + FontAssetPath);
            return;
        }

        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError("[MenuFontAtlasBuilder] Source TTF not found at " + SourceFontPath);
            return;
        }

        var serialized = new SerializedObject(fontAsset);
        serialized.FindProperty("m_SourceFontFile").objectReferenceValue = sourceFont;
        serialized.FindProperty("m_SourceFontFileGUID").stringValue =
            AssetDatabase.AssetPathToGUID(SourceFontPath);
        serialized.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Dynamic;
        serialized.FindProperty("m_AtlasWidth").intValue = AtlasSize;
        serialized.FindProperty("m_AtlasHeight").intValue = AtlasSize;
        serialized.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = true;

        // Keep the baked glyphs in player builds so Turkish text never falls back to LiberationSans.
        serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        fontAsset.ClearFontAssetData(true);

        uint[] unicodes = BuildCharacterSet();
        bool added = fontAsset.TryAddCharacters(unicodes, out uint[] missing);

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);

        if (!added)
        {
            Debug.LogError("[MenuFontAtlasBuilder] Failed to add glyphs to " + fontAsset.name);
            return;
        }

        Debug.Log(
            "[MenuFontAtlasBuilder] Rebuilt " + fontAsset.name + ".\n"
            + "  Requested glyphs: " + unicodes.Length + "\n"
            + "  Missing in source font: " + (missing == null ? 0 : missing.Length) + "\n"
            + "  Atlas: " + AtlasSize + "x" + AtlasSize + " (multi-atlas on, Dynamic population)\n"
            + "  Turkish glyphs now baked: " + DescribeTurkishCoverage(fontAsset));
    }

    static uint[] BuildCharacterSet()
    {
        var unicodes = new List<uint>();

        AddRange(unicodes, 0x0020, 0x007E); // Basic Latin
        AddRange(unicodes, 0x00A0, 0x00FF); // Latin-1 Supplement (Ç ç Ö ö Ü ü and European accents)
        AddRange(unicodes, 0x011E, 0x0131); // Ğ ğ İ ı
        AddRange(unicodes, 0x015E, 0x015F); // Ş ş

        uint[] punctuation =
        {
            0x2013, 0x2014, // en/em dash
            0x2018, 0x2019, 0x201C, 0x201D, // curly quotes
            0x2022, // bullet used by the roadmap list
            0x2026, // ellipsis
            0x20AC, 0x20BA // euro, Turkish lira
        };

        unicodes.AddRange(punctuation);
        return unicodes.ToArray();
    }

    static void AddRange(List<uint> unicodes, uint first, uint last)
    {
        for (uint code = first; code <= last; code++)
            unicodes.Add(code);
    }

    static string DescribeTurkishCoverage(TMP_FontAsset fontAsset)
    {
        const string turkish = "ÇçĞğİıÖöŞşÜü";
        var missing = new List<char>();

        for (int i = 0; i < turkish.Length; i++)
        {
            if (!fontAsset.characterLookupTable.ContainsKey(turkish[i]))
                missing.Add(turkish[i]);
        }

        return missing.Count == 0 ? "all present" : "still missing " + new string(missing.ToArray());
    }
}

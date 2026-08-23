using TMPro;
using UnityEngine;

/// <summary>
/// Player-facing UI always uses Baloo 2 ExtraBold. Noto (Cyrillic / CJK) is not assigned
/// per-label — TMP walks Baloo's fallback table when a glyph is missing.
/// </summary>
public static class UiMenuFont
{
    const string PrimaryName = "Baloo2-ExtraBold SDF";
    const string OutlineThinName = "Baloo2-ExtraBold SDF - Outline Thin";
    const string OutlineThinPath = "Assets/TextMesh Pro/Fonts/Materials/Baloo2-ExtraBold SDF - Outline Thin.mat";

    static TMP_FontAsset _font;
    static Material _outlineThin;

    public static TMP_FontAsset Font
    {
        get
        {
            if (_font != null)
                return _font;

            TMP_FontAsset settingsDefault = TMP_Settings.defaultFontAsset;
            if (IsPrimary(settingsDefault))
            {
                _font = settingsDefault;
                return _font;
            }

            TMP_FontAsset[] loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            TMP_FontAsset anyBaloo = null;
            for (int i = 0; i < loaded.Length; i++)
            {
                TMP_FontAsset candidate = loaded[i];
                if (!IsPrimary(candidate))
                    continue;

                if (candidate.name == PrimaryName)
                {
                    _font = candidate;
                    return _font;
                }

                anyBaloo ??= candidate;
            }

            _font = anyBaloo;
            return _font;
        }
    }

    public static bool IsPrimary(TMP_FontAsset font)
    {
        return font != null
               && font.name.IndexOf("Baloo", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static Material OutlineThin
    {
        get
        {
            if (_outlineThin != null)
                return _outlineThin;

#if UNITY_EDITOR
            _outlineThin = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(OutlineThinPath);
            if (_outlineThin != null)
                return _outlineThin;
#endif

            Material[] loaded = Resources.FindObjectsOfTypeAll<Material>();
            for (int i = 0; i < loaded.Length; i++)
            {
                Material candidate = loaded[i];
                if (candidate != null && candidate.name == OutlineThinName)
                {
                    _outlineThin = candidate;
                    return _outlineThin;
                }
            }

            return null;
        }
    }

    public static void Apply(TMP_Text text)
    {
        TMP_FontAsset font = Font;
        if (text == null || font == null)
            return;

        if (text.font != font && !IsPrimary(text.font))
            text.font = font;
    }

    /// <summary>Right-side control values: Baloo + Outline Thin preset. Does not set outlineWidth.</summary>
    public static void ApplyControl(TMP_Text text)
    {
        Apply(text);
        Material material = OutlineThin;
        if (text == null || material == null)
            return;

        text.fontSharedMaterial = material;
    }

    public static void ApplyToHierarchy(Transform root)
    {
        if (root == null)
            return;

        TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
            Apply(labels[i]);
    }
}

using TMPro;
using UnityEngine;

/// <summary>
/// Keeps TMP labels inside their RectTransform. The authored size is the maximum (short copy stays
/// at design size); longer translations shrink instead of overflowing the button or panel.
/// </summary>
public static class UiTextFit
{
    public const float DefaultMinScale = 0.5f;
    public const float AbsoluteMinSize = 10f;

    public static void Apply(TMP_Text text, float minScale = DefaultMinScale)
    {
        if (text == null)
            return;

        float designed = ResolveDesignedSize(text);
        float min = Mathf.Max(AbsoluteMinSize, designed * Mathf.Clamp01(minScale));
        if (min > designed)
            min = designed;

        text.enableAutoSizing = true;
        text.fontSizeMax = designed;
        text.fontSizeMin = min;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.ForceMeshUpdate();
    }

    static float ResolveDesignedSize(TMP_Text text)
    {
        if (text.enableAutoSizing && text.fontSizeMax > 1f)
            return text.fontSizeMax;

        if (text.fontSize > 1f)
            return text.fontSize;

        return 24f;
    }
}

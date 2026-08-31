using UnityEngine;

/// <summary>
/// Keeps inspect preview cards/packs inside the visible canvas on any resolution.
/// The authored offset (right of center on 1920x1080) is preserved when it fits.
/// </summary>
public static class InspectPreviewScreenFit
{
    const float PlatePadding = 18f;
    const float ScreenMargin = 24f;

    public static void Apply(RectTransform previewRoot, Vector2 preferredSize, Vector2 preferredOffsetFromCenter)
    {
        if (previewRoot == null)
            return;

        var canvasRect = previewRoot.parent as RectTransform;
        if (canvasRect == null)
            return;

        Vector2 canvas = canvasRect.rect.size;
        if (canvas.x <= 1f || canvas.y <= 1f)
            return;

        float maxWidth = Mathf.Max(1f, canvas.x - (ScreenMargin + PlatePadding) * 2f);
        float maxHeight = Mathf.Max(1f, canvas.y - (ScreenMargin + PlatePadding) * 2f);
        float scale = Mathf.Min(1f, maxWidth / preferredSize.x, maxHeight / preferredSize.y);
        Vector2 size = preferredSize * scale;
        previewRoot.sizeDelta = size;

        float halfW = size.x * 0.5f + PlatePadding;
        float halfH = size.y * 0.5f + PlatePadding;
        float minX = -canvas.x * 0.5f + ScreenMargin + halfW;
        float maxX = canvas.x * 0.5f - ScreenMargin - halfW;
        float minY = -canvas.y * 0.5f + ScreenMargin + halfH;
        float maxY = canvas.y * 0.5f - ScreenMargin - halfH;

        float x = minX <= maxX
            ? Mathf.Clamp(preferredOffsetFromCenter.x, minX, maxX)
            : 0f;
        float y = minY <= maxY
            ? Mathf.Clamp(preferredOffsetFromCenter.y, minY, maxY)
            : 0f;

        previewRoot.anchoredPosition = new Vector2(x, y);
    }
}

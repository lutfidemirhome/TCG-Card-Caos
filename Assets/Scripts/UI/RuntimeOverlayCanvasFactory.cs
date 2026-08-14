using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared screen-space overlay canvas setup for runtime UI (crosshair, prompts, previews).
/// </summary>
public static class RuntimeOverlayCanvasFactory
{
    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;

    public static Canvas Create(Transform parent, string objectName, int sortingOrder, float matchWidthOrHeight = 0f)
    {
        var canvasGo = new GameObject(objectName);
        canvasGo.transform.SetParent(parent, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = matchWidthOrHeight;

        canvasGo.AddComponent<GraphicRaycaster>();
        return canvas;
    }
}

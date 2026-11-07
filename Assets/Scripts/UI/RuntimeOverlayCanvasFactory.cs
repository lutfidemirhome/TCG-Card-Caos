using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared screen-space overlay canvas setup for runtime UI (crosshair, prompts, previews).
/// </summary>
public static class RuntimeOverlayCanvasFactory
{
    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;

    // Keep the entire 16:9 design visible. Extra width/height expands the canvas instead
    // of enlarging the controls until fixed-height panels extend beyond the screen.
    public static void ConfigureScreenScaling(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    }

    public static Canvas Create(Transform parent, string objectName, int sortingOrder)
    {
        var canvasGo = new GameObject(objectName);
        canvasGo.transform.SetParent(parent, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        ConfigureScreenScaling(scaler);

        canvasGo.AddComponent<GraphicRaycaster>();
        return canvas;
    }
}

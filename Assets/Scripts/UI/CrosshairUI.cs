using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Center-screen dot reticle (Librarian-style).
/// </summary>
public class CrosshairUI : MonoBehaviour
{
    [SerializeField] float dotSize = 14f;
    [SerializeField] Color color = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] bool hideWhenCursorUnlocked = true;

    Canvas _canvas;

    void Awake()
    {
        BuildCrosshair();
    }

    void LateUpdate()
    {
        if (_canvas != null && hideWhenCursorUnlocked)
            _canvas.enabled = Cursor.lockState == CursorLockMode.Locked;
    }

    void BuildCrosshair()
    {
        var canvasGo = new GameObject("CrosshairCanvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGo.AddComponent<GraphicRaycaster>();

        var dotGo = new GameObject("CrosshairDot");
        dotGo.transform.SetParent(canvasGo.transform, false);

        var dot = dotGo.AddComponent<Image>();
        dot.sprite = CreateDotSprite(32, color);
        dot.raycastTarget = false;

        RectTransform rect = dot.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(dotSize, dotSize);
    }

    static Sprite CreateDotSprite(int textureSize, Color dotColor)
    {
        var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        float center = textureSize * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float dx = x + 0.5f - center;
                float dy = y + 0.5f - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                texture.SetPixel(x, y, distance <= radius ? dotColor : Color.clear);
            }
        }

        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            100f);
    }
}

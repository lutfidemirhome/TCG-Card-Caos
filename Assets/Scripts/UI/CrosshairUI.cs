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
    Sprite _dotSprite;
    Texture2D _dotTexture;
    Image _dot;
    InteractionController _interaction;
    static readonly Color InteractionColor = new Color32(75, 230, 75, 242);
    static readonly Color CabinetColor = new Color32(255, 220, 45, 242);

    void Awake()
    {
        BuildCrosshair();
    }

    void Start() => _interaction = GetComponent<InteractionController>();

    void LateUpdate()
    {
        Color targetColor = color;
        if (_interaction != null && _interaction.isActiveAndEnabled)
        {
            if (_interaction.CurrentReticleTarget == InteractionController.ReticleTarget.Cabinet)
                targetColor = CabinetColor;
            else if (_interaction.CurrentReticleTarget == InteractionController.ReticleTarget.Interactable)
                targetColor = InteractionColor;
        }
        if (_dot != null && _dot.color != targetColor) _dot.color = targetColor;
        if (_canvas != null && hideWhenCursorUnlocked)
        {
            bool visible = Cursor.lockState == CursorLockMode.Locked;
            if (_canvas.enabled != visible)
                _canvas.enabled = visible;
        }
    }

    void BuildCrosshair()
    {
        _canvas = RuntimeOverlayCanvasFactory.Create(transform, "CrosshairCanvas", sortingOrder: 100);

        var dotGo = new GameObject("CrosshairDot");
        dotGo.transform.SetParent(_canvas.transform, false);

        var dot = dotGo.AddComponent<Image>();
        _dot = dot;
        _dotSprite = CreateDotSprite(32, Color.white);
        _dotTexture = _dotSprite.texture;
        dot.sprite = _dotSprite;
        dot.color = color;
        dot.raycastTarget = false;

        RectTransform rect = dot.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(dotSize, dotSize) * 1.4f;
    }

    void OnDestroy()
    {
        // These assets belong to this instance, not to the project or another canvas.
        if (_dotSprite != null) Destroy(_dotSprite);
        if (_dotTexture != null) Destroy(_dotTexture);
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

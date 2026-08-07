using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space (2D UI) inspect preview for the looked-at ground card.
/// Always faces the player like the interaction prompt — no world-space tilt.
/// </summary>
public class CardInspectPreview : MonoBehaviour
{
    [SerializeField] Vector2 anchoredPosition = new Vector2(620f, 40f);
    [SerializeField] Vector2 cardSize = new Vector2(320f, 448f);

    Canvas _canvas;
    RectTransform _previewRoot;
    RawImage _cardImage;
    int _shownPaletteIndex = int.MinValue;

    public static CardInspectPreview EnsureOn(Camera camera)
    {
        if (camera == null)
            return null;

        var preview = camera.GetComponent<CardInspectPreview>();
        if (preview != null)
            return preview;

        return camera.gameObject.AddComponent<CardInspectPreview>();
    }

    void Awake()
    {
        BuildPreviewUI();
        Hide();
    }

    public void Show(WorldCard card)
    {
        if (card == null || card.IsInHand)
        {
            Hide();
            return;
        }

        EnsurePreviewUI();
        ApplyPalette(card.PaletteIndex);

        if (_previewRoot != null)
            _previewRoot.gameObject.SetActive(true);
    }

    public void Hide()
    {
        _shownPaletteIndex = int.MinValue;
        if (_previewRoot != null)
            _previewRoot.gameObject.SetActive(false);
    }

    void BuildPreviewUI()
    {
        if (_previewRoot != null)
            return;

        var canvasGo = new GameObject("CardInspectPreviewCanvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 95;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        var rootGo = new GameObject("InspectCard");
        rootGo.transform.SetParent(canvasGo.transform, false);

        _previewRoot = rootGo.AddComponent<RectTransform>();
        _previewRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _previewRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _previewRoot.pivot = new Vector2(0.5f, 0.5f);
        _previewRoot.anchoredPosition = anchoredPosition;
        _previewRoot.sizeDelta = cardSize;

        // Soft dark plate behind the card so art pops against busy scenes.
        var plateGo = new GameObject("Plate");
        plateGo.transform.SetParent(rootGo.transform, false);
        var plateImage = plateGo.AddComponent<Image>();
        plateImage.color = new Color(0f, 0f, 0f, 0.45f);
        plateImage.raycastTarget = false;
        RectTransform plateRect = plateImage.rectTransform;
        plateRect.anchorMin = Vector2.zero;
        plateRect.anchorMax = Vector2.one;
        plateRect.offsetMin = new Vector2(-18f, -18f);
        plateRect.offsetMax = new Vector2(18f, 18f);

        var cardGo = new GameObject("CardArt");
        cardGo.transform.SetParent(rootGo.transform, false);
        _cardImage = cardGo.AddComponent<RawImage>();
        _cardImage.raycastTarget = false;
        _cardImage.color = Color.white;
        RectTransform cardRect = _cardImage.rectTransform;
        cardRect.anchorMin = Vector2.zero;
        cardRect.anchorMax = Vector2.one;
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;
    }

    void EnsurePreviewUI()
    {
        if (_previewRoot == null)
            BuildPreviewUI();
    }

    void ApplyPalette(int paletteIndex)
    {
        if (_cardImage == null)
            return;

        if (_shownPaletteIndex == paletteIndex && _cardImage.texture != null)
            return;

        CardArtLibrary.EnsureLoaded();
        Material front = CardArtLibrary.GetFrontMaterial(paletteIndex, CardTextureQuality.Detail);
        Texture texture = front != null ? front.GetTexture("_BaseMap") : null;
        if (texture == null && front != null)
            texture = front.mainTexture;

        _cardImage.texture = texture;
        _cardImage.uvRect = CardArtLibrary.FrontArtUvRect;
        _shownPaletteIndex = paletteIndex;
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space inspect preview for a looked-at ground PSA slab.
/// Uses upright preview PNGs (psa_7_1_Preview, etc.), not the 3D slab atlas.
/// </summary>
public class PsaInspectPreview : MonoBehaviour
{
    [SerializeField] Vector2 anchoredPosition = new Vector2(620f, 40f);
    [SerializeField] Vector2 previewSize = new Vector2(416f, 582.4f);

    Canvas _canvas;
    RectTransform _previewRoot;
    RawImage _previewImage;
    string _shownKey;

    public static PsaInspectPreview EnsureOn(Camera camera)
    {
        if (camera == null)
            return null;

        var preview = camera.GetComponent<PsaInspectPreview>();
        if (preview != null)
            return preview;

        return camera.gameObject.AddComponent<PsaInspectPreview>();
    }

    void Awake()
    {
        BuildPreviewUI();
        Hide();
    }

    public void Show(WorldCard card)
    {
        if (card == null || !card.UsesPsaSlab || card.IsInHand)
        {
            Hide();
            return;
        }

        EnsurePreviewUI();
        if (_previewRoot != null)
            _previewRoot.gameObject.SetActive(true);

        ApplyPreviewArt(card);
        FitToScreen();
    }

    public void Hide()
    {
        _shownKey = null;
        if (_previewRoot != null)
            _previewRoot.gameObject.SetActive(false);
    }

    void BuildPreviewUI()
    {
        if (_previewRoot != null)
            return;

        _canvas = RuntimeOverlayCanvasFactory.Create(transform, "PsaInspectPreviewCanvas", sortingOrder: 95, matchWidthOrHeight: 0.5f);

        var rootGo = new GameObject("InspectPsa");
        rootGo.transform.SetParent(_canvas.transform, false);

        _previewRoot = rootGo.AddComponent<RectTransform>();
        _previewRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _previewRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _previewRoot.pivot = new Vector2(0.5f, 0.5f);
        _previewRoot.sizeDelta = previewSize;
        FitToScreen();

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

        var artGo = new GameObject("PsaArt");
        artGo.transform.SetParent(rootGo.transform, false);
        _previewImage = artGo.AddComponent<RawImage>();
        _previewImage.raycastTarget = false;
        _previewImage.color = Color.white;
        RectTransform artRect = _previewImage.rectTransform;
        artRect.anchorMin = Vector2.zero;
        artRect.anchorMax = Vector2.one;
        artRect.offsetMin = Vector2.zero;
        artRect.offsetMax = Vector2.zero;
    }

    void EnsurePreviewUI()
    {
        if (_previewRoot == null)
            BuildPreviewUI();
    }

    void ApplyPreviewArt(WorldCard card)
    {
        if (_previewImage == null || card == null || !card.UsesPsaSlab)
            return;

        int slotNumber = card.PsaSlotNumber;
        int variantIndex = card.PsaVariantIndex;
        string key = slotNumber + ":" + variantIndex;
        Texture texture = PsaArtLibrary.GetVariantPreview(slotNumber, variantIndex);
        if (texture == null)
        {
            Hide();
            return;
        }

        if (key == _shownKey && _previewImage.texture == texture)
            return;

        _previewImage.texture = texture;
        _previewImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        _shownKey = key;
    }

    void LateUpdate()
    {
        if (_previewRoot != null && _previewRoot.gameObject.activeSelf)
            FitToScreen();
    }

    void FitToScreen()
    {
        InspectPreviewScreenFit.Apply(_previewRoot, previewSize, anchoredPosition);
    }
}

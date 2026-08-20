using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space inspect preview for a looked-at ground booster pack.
/// Uses dedicated upright preview PNGs (Pack0N_Preview), not the 3D foil atlas.
/// </summary>
public class PackInspectPreview : MonoBehaviour
{
    [SerializeField] Vector2 anchoredPosition = new Vector2(620f, 40f);
    [SerializeField] Vector2 packSize = new Vector2(416f, 582.4f);

    Canvas _canvas;
    RectTransform _previewRoot;
    RawImage _packImage;
    int _shownVariantIndex;

    public static PackInspectPreview EnsureOn(Camera camera)
    {
        if (camera == null)
            return null;

        var preview = camera.GetComponent<PackInspectPreview>();
        if (preview != null)
            return preview;

        return camera.gameObject.AddComponent<PackInspectPreview>();
    }

    void Awake()
    {
        BuildPreviewUI();
        Hide();
    }

    public void Show(WorldBoosterPack pack)
    {
        if (pack == null || pack.IsInHand)
        {
            Hide();
            return;
        }

        EnsurePreviewUI();
        if (_previewRoot != null)
            _previewRoot.gameObject.SetActive(true);

        ApplyPackArt(pack);
    }

    public void Hide()
    {
        _shownVariantIndex = 0;
        if (_previewRoot != null)
            _previewRoot.gameObject.SetActive(false);
    }

    void BuildPreviewUI()
    {
        if (_previewRoot != null)
            return;

        _canvas = RuntimeOverlayCanvasFactory.Create(transform, "PackInspectPreviewCanvas", sortingOrder: 95, matchWidthOrHeight: 0.5f);

        var rootGo = new GameObject("InspectPack");
        rootGo.transform.SetParent(_canvas.transform, false);

        _previewRoot = rootGo.AddComponent<RectTransform>();
        _previewRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _previewRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _previewRoot.pivot = new Vector2(0.5f, 0.5f);
        _previewRoot.anchoredPosition = anchoredPosition;
        _previewRoot.sizeDelta = packSize;

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

        var packGo = new GameObject("PackArt");
        packGo.transform.SetParent(rootGo.transform, false);
        _packImage = packGo.AddComponent<RawImage>();
        _packImage.raycastTarget = false;
        _packImage.color = Color.white;
        RectTransform packRect = _packImage.rectTransform;
        packRect.anchorMin = Vector2.zero;
        packRect.anchorMax = Vector2.one;
        packRect.offsetMin = Vector2.zero;
        packRect.offsetMax = Vector2.zero;
    }

    void EnsurePreviewUI()
    {
        if (_previewRoot == null)
            BuildPreviewUI();
    }

    void ApplyPackArt(WorldBoosterPack pack)
    {
        if (_packImage == null || pack == null)
            return;

        int variantIndex = pack.PackVariantIndex;
        Texture texture = PackArtLibrary.GetVariantPreview(variantIndex);
        if (texture == null)
        {
            Hide();
            return;
        }

        if (variantIndex == _shownVariantIndex && _packImage.texture == texture)
            return;

        _packImage.texture = texture;
        _packImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        _shownVariantIndex = variantIndex;
    }
}

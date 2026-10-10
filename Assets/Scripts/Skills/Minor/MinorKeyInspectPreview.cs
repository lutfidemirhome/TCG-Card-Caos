using UnityEngine;
using UnityEngine.UI;

/// <summary>One cached UI image; the model is rendered offline by the editor builder.</summary>
public sealed class MinorKeyInspectPreview : MonoBehaviour
{
    Image _image;
    static readonly Vector2 Size = new Vector2(330, 495);
    static readonly Vector2 Position = new Vector2(620, 40);
    public static MinorKeyInspectPreview EnsureOn(Camera camera)
    {
        if (camera == null) return null;
        return camera.GetComponent<MinorKeyInspectPreview>() ?? camera.gameObject.AddComponent<MinorKeyInspectPreview>();
    }
    public void Show(WorldMinorSkillKey key)
    {
        if (key == null || key.IsHeld || key.InspectSprite == null) { Hide(); return; }
        if (_image == null)
        {
            var canvas = RuntimeOverlayCanvasFactory.Create(transform, "KeyInspectPreviewCanvas", sortingOrder: 95, matchWidthOrHeight: .5f);
            var go = new GameObject("KeyArt", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            _image = go.GetComponent<Image>();
            _image.raycastTarget = false;
            _image.preserveAspect = true;
        }
        if (_image.sprite != key.InspectSprite) _image.sprite = key.InspectSprite;
        if (!_image.gameObject.activeSelf) _image.gameObject.SetActive(true);
        Fit();
    }
    public void Hide() { if (_image != null && _image.gameObject.activeSelf) _image.gameObject.SetActive(false); }
    void LateUpdate() { if (_image != null && _image.gameObject.activeSelf) Fit(); }
    void Fit() => InspectPreviewScreenFit.Apply(_image.rectTransform, Size, Position);
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps the load-game scrollbar thumb a fixed circle that travels along the track
/// instead of stretching like Unity's default scrollbar handle.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Scrollbar))]
public class LoadGameCircularScrollThumb : MonoBehaviour
{
    [SerializeField] Scrollbar scrollbar;
    [SerializeField] RectTransform slidingArea;
    [SerializeField] RectTransform handle;
    [SerializeField] float thumbSize = 88f;

    Image handleImage;

    void Awake()
    {
        if (scrollbar == null)
            scrollbar = GetComponent<Scrollbar>();

        if (handleImage == null && handle != null)
            handleImage = handle.GetComponent<Image>();

        if (handleImage != null)
            handleImage.preserveAspect = true;
    }

    void LateUpdate()
    {
        ApplyFixedThumb();
    }

    void OnRectTransformDimensionsChange()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;

        ApplyFixedThumb();
    }

    void ApplyFixedThumb()
    {
        if (scrollbar == null || slidingArea == null || handle == null)
            return;

        float areaHeight = slidingArea.rect.height;
        if (areaHeight <= 1f)
            return;

        float size = Mathf.Clamp(thumbSize / areaHeight, 0.04f, 0.35f);
        float travel = 1f - size;
        float minY = scrollbar.value * travel;

        handle.anchorMin = new Vector2(0f, minY);
        handle.anchorMax = new Vector2(1f, minY + size);
        handle.offsetMin = Vector2.zero;
        handle.offsetMax = Vector2.zero;
        handle.pivot = new Vector2(0.5f, 0.5f);
        handle.localScale = Vector3.one;
    }

}

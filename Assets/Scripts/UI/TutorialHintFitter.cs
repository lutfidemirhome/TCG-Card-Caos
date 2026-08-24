using TMPro;
using UnityEngine;

/// <summary>
/// Grows <see cref="TutorialHintView.PanelName"/> with the current language string.
/// Top-right pivot: width grows left, height grows down. Does not shrink text to fit.
/// </summary>
[DisallowMultipleComponent]
public class TutorialHintFitter : MonoBehaviour
{
    [SerializeField] TMP_Text label;
    [SerializeField] float maxWidth = 1000f;
    [SerializeField] float paddingLeft = 28f;
    [SerializeField] float paddingRight = 22f;
    [SerializeField] float paddingTop = 18f;
    [SerializeField] float paddingBottom = 18f;

    RectTransform _panel;
    bool _relayouting;

    void Awake() => Bind();

    void OnEnable()
    {
        Bind();
        Relayout();
    }

    public void Relayout()
    {
        if (_relayouting)
            return;

        Bind();
        if (_panel == null || label == null)
            return;

        _relayouting = true;
        try
        {
            float cap = Mathf.Max(120f, maxWidth);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cap);
            label.ForceMeshUpdate();

            Vector2 unconstrained = label.GetPreferredValues(99999f, 0f);
            float textW = Mathf.Min(cap, Mathf.Max(1f, unconstrained.x));
            Vector2 wrapped = label.GetPreferredValues(textW, 0f);
            float textH = Mathf.Max(1f, wrapped.y);

            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = new Vector2(paddingLeft, -paddingTop);
            labelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textW);
            labelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textH);
            label.ForceMeshUpdate();

            _panel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                textW + paddingLeft + paddingRight);
            _panel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                textH + paddingTop + paddingBottom);
        }
        finally
        {
            _relayouting = false;
        }
    }

    void Bind()
    {
        if (_panel == null)
            _panel = transform as RectTransform;

        if (label == null)
        {
            Transform found = transform.Find(TutorialHintView.LabelName);
            if (found != null)
                label = found.GetComponent<TMP_Text>();
        }
    }
}

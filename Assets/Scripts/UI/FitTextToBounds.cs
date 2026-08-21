using TMPro;
using UnityEngine;

/// <summary>
/// Attach next to any TMP label that must stay inside its parent (buttons, panels, headers).
/// Short text keeps the authored font size; long text shrinks. LocalizedText enables this
/// automatically so every translated string gets the same rule.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class FitTextToBounds : MonoBehaviour
{
    [Tooltip("Smallest allowed size as a fraction of the authored font size.")]
    [Range(0.25f, 1f)]
    [SerializeField] float minScale = UiTextFit.DefaultMinScale;

    TMP_Text _text;

    void Awake() => EnsureText();

    void OnEnable() => ApplyFit();

    void OnRectTransformDimensionsChange() => ApplyFit();

#if UNITY_EDITOR
    void OnValidate() => ApplyFit();
#endif

    bool _applying;

    public void ApplyFit()
    {
        if (_applying)
            return;

        EnsureText();
        _applying = true;
        try
        {
            UiTextFit.Apply(_text, minScale);
        }
        finally
        {
            _applying = false;
        }
    }

    void EnsureText()
    {
        if (_text == null)
            _text = GetComponent<TMP_Text>();
    }
}

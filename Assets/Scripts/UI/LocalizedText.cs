using TMPro;
using UnityEngine;

/// <summary>
/// Drives a TextMeshPro label from a <see cref="LocalizationKeys"/> entry so the string updates
/// whenever the language changes. Attach next to any TMP text that shows player-facing copy.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("Key from LocalizationKeys, e.g. menu.new_game")]
    [SerializeField] string key;

    [Tooltip("Uppercases the result. Use for headers instead of baking caps into translations.")]
    [SerializeField] bool uppercase;

    TMP_Text _text;

    public string Key => key;

    void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        Localization.LanguageChanged += Apply;
        Apply();
    }

    void OnDisable() => Localization.LanguageChanged -= Apply;

    public void SetKey(string localizationKey)
    {
        key = localizationKey;
        Apply();
    }

    void Apply()
    {
        if (_text == null)
            _text = GetComponent<TMP_Text>();

        if (_text == null || string.IsNullOrEmpty(key))
            return;

        ApplyValue(Localization.Get(key));
    }

    void ApplyValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        // Unity YAML sometimes stores literal "\n" instead of line breaks; keep lists itemized.
        value = value.Replace("\\n", "\n").Replace("\r\n", "\n");
        _text.text = uppercase
            ? GameLanguages.ToUpper(value, Localization.CurrentLanguage)
            : value;

        FitTextToBounds fit = GetComponent<FitTextToBounds>();
        if (fit != null)
            fit.ApplyFit();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying)
            return;

        if (_text == null)
            _text = GetComponent<TMP_Text>();

        if (_text == null || string.IsNullOrEmpty(key))
            return;

        ApplyValue(Localization.Get(key));
    }
#endif
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One loading overlay for every path: menu Continue/New Game and in-game Load Save.
/// Runtime always instantiates Resources/UI/LoadingScreen so both look the same.
/// Scene copies in MenuScene / MainScene are for editing only.
/// Only SpinnerYellow rotates.
/// Boot-to-menu also uses this overlay and shows the fiction disclaimer.
/// </summary>
public class LoadingScreenUI : MonoBehaviour
{
    const float SpinnerSpeed = 220f;
    const float DotStepDuration = 0.35f;
    const int SortingOrder = 5000;
    const string PrefabResourcePath = "UI/LoadingScreen";
    const string DisclaimerObjectName = "Disclaimer";

    static readonly Color LabelColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    static readonly Color DisclaimerColor = Color.white;
    static readonly Vector2 DisclaimerSize = new Vector2(1720f, 118f);
    const float DisclaimerGapBelowLogo = 12f;
    const float DisclaimerFontSize = 26f;

    static LoadingScreenUI _instance;

    [SerializeField] GameObject root;
    [SerializeField] RectTransform spinnerYellow;
    [SerializeField] TMP_Text label;
    [SerializeField] TMP_Text disclaimer;
    [SerializeField] bool editorPreview;

    float _showTime;
    bool _showDisclaimer;

    public static LoadingScreenUI Instance => _instance;

    public static LoadingScreenUI Ensure()
    {
        if (_instance != null)
            return _instance;

        GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab != null)
        {
            GameObject instance = Object.Instantiate(prefab);
            instance.name = "LoadingCanvas";
            if (Application.isPlaying)
                Object.DontDestroyOnLoad(instance);

            _instance = instance.GetComponent<LoadingScreenUI>();
            if (_instance == null)
                _instance = instance.AddComponent<LoadingScreenUI>();

            _instance.editorPreview = false;
            return _instance;
        }

        LoadingScreenUI existing = FindFirstObjectByType<LoadingScreenUI>(FindObjectsInactive.Include);
        if (existing != null && !existing.editorPreview)
        {
            _instance = existing;
            if (Application.isPlaying)
                DontDestroyOnLoad(existing.gameObject);
            return _instance;
        }

        Debug.LogError("[LoadingScreenUI] Resources/UI/LoadingScreen prefab is missing.");
        return null;
    }

    void Awake()
    {
        if (editorPreview && Application.isPlaying)
        {
            gameObject.SetActive(false);
            return;
        }

        if (_instance == null)
            _instance = this;

        Bind();
    }

    void OnEnable()
    {
        Localization.LanguageChanged += ApplyDisclaimerText;
    }

    void OnDisable()
    {
        Localization.LanguageChanged -= ApplyDisclaimerText;
    }

    void Bind()
    {
        if (root == null)
        {
            Transform found = transform.Find("Panel_Loading");
            if (found != null)
                root = found.gameObject;
        }

        if (spinnerYellow == null)
        {
            Transform found = transform.Find("Panel_Loading/Spinner/SpinnerYellow");
            if (found != null)
                spinnerYellow = found as RectTransform;
        }

        if (label == null)
        {
            Transform found = transform.Find("Panel_Loading/Label");
            if (found != null)
                label = found.GetComponent<TMP_Text>();
        }

        EnsureDisclaimer();
    }

    void EnsureDisclaimer()
    {
        if (disclaimer != null)
            return;

        Transform found = transform.Find("Panel_Loading/" + DisclaimerObjectName);
        if (found != null)
        {
            disclaimer = found.GetComponent<TMP_Text>();
            return;
        }

        if (root == null)
            return;

        var go = new GameObject(DisclaimerObjectName, typeof(RectTransform));
        go.transform.SetParent(root.transform, false);

        Transform logo = root.transform.Find("Logo");
        if (logo != null)
            go.transform.SetSiblingIndex(logo.GetSiblingIndex() + 1);

        LayoutDisclaimerRect((RectTransform)go.transform);

        disclaimer = go.AddComponent<TextMeshProUGUI>();
        go.SetActive(false);
    }

    public void Show()
    {
        ShowInternal(showDisclaimer: false);
    }

    public void ShowBoot()
    {
        ShowInternal(showDisclaimer: true);
    }

    void ShowInternal(bool showDisclaimer)
    {
        Bind();
        _showTime = Time.unscaledTime;
        _showDisclaimer = showDisclaimer;

        gameObject.SetActive(true);
        if (root != null)
            root.SetActive(true);

        transform.SetAsLastSibling();
        ForceOverlayCanvas();

        PrepareLabel();
        UpdateLabel();
        PrepareDisclaimer();
        ApplyDisclaimerText();
    }

    public void Hide()
    {
        _showDisclaimer = false;
        if (disclaimer != null)
            disclaimer.gameObject.SetActive(false);

        if (root != null)
            root.SetActive(false);

        gameObject.SetActive(false);
    }

    void ForceOverlayCanvas()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;
        canvas.enabled = true;
        canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1
            | AdditionalCanvasShaderChannels.Normal
            | AdditionalCanvasShaderChannels.Tangent;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(
            RuntimeOverlayCanvasFactory.ReferenceWidth,
            RuntimeOverlayCanvasFactory.ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.enabled = false;
        scaler.enabled = true;

        Canvas.ForceUpdateCanvases();
        ApplyFallbackOverlaySize(scaler);
    }

    void ApplyFallbackOverlaySize(CanvasScaler scaler)
    {
        var rect = transform as RectTransform;
        if (rect == null)
            return;

        bool uninitialized = rect.localScale.sqrMagnitude < 1e-6f
                             || (rect.sizeDelta.x < 1f && rect.sizeDelta.y < 1f);
        if (!uninitialized)
            return;

        float screenW = Mathf.Max(1, Screen.width);
        float screenH = Mathf.Max(1, Screen.height);
        float refW = scaler != null ? scaler.referenceResolution.x : RuntimeOverlayCanvasFactory.ReferenceWidth;
        float refH = scaler != null ? scaler.referenceResolution.y : RuntimeOverlayCanvasFactory.ReferenceHeight;
        float match = scaler != null ? scaler.matchWidthOrHeight : 0.5f;
        float scale = Mathf.Lerp(screenW / refW, screenH / refH, match);
        if (scale < 0.0001f)
            scale = 1f;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = new Vector3(scale, scale, scale);
        rect.sizeDelta = new Vector2(screenW / scale, screenH / scale);
    }

    void PrepareLabel()
    {
        if (label == null)
            return;

        LocalizedText localized = label.GetComponent<LocalizedText>();
        if (localized != null)
            localized.enabled = false;

        label.gameObject.SetActive(true);
        label.enabled = true;
        label.raycastTarget = false;
        label.richText = false;
        label.enableAutoSizing = false;
        label.fontSize = 36f;
        label.alignment = TextAlignmentOptions.Center;
        label.overflowMode = TextOverflowModes.Overflow;
        label.color = LabelColor;
        ApplyDefaultFont(label);
        label.ForceMeshUpdate();
    }

    void PrepareDisclaimer()
    {
        if (disclaimer == null)
            return;

        LocalizedText localized = disclaimer.GetComponent<LocalizedText>();
        if (localized != null)
            localized.enabled = false;

        disclaimer.gameObject.SetActive(_showDisclaimer);
        if (!_showDisclaimer)
            return;

        LayoutDisclaimerRect(disclaimer.rectTransform);

        disclaimer.enabled = true;
        disclaimer.raycastTarget = false;
        disclaimer.richText = false;
        disclaimer.enableAutoSizing = false;
        disclaimer.fontSize = DisclaimerFontSize;
        disclaimer.horizontalAlignment = HorizontalAlignmentOptions.Center;
        disclaimer.verticalAlignment = VerticalAlignmentOptions.Top;
        disclaimer.overflowMode = TextOverflowModes.Overflow;
        disclaimer.textWrappingMode = TextWrappingModes.Normal;
        disclaimer.color = DisclaimerColor;
        ApplyDefaultFont(disclaimer);
    }

    void LayoutDisclaimerRect(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = DisclaimerSize;

        float topY = -90f;
        if (root != null)
        {
            Transform logo = root.transform.Find("Logo");
            if (logo is RectTransform logoRect)
                topY = logoRect.anchoredPosition.y - logoRect.sizeDelta.y * 0.5f;
        }

        rect.anchoredPosition = new Vector2(0f, topY - DisclaimerGapBelowLogo);
    }

    static void ApplyDefaultFont(TMP_Text text)
    {
        TMP_FontAsset font = UiMenuFont.Font;
        if (text == null || font == null)
            return;

        text.font = font;
        if (font.material != null)
            text.fontSharedMaterial = font.material;
    }

    void ApplyDisclaimerText()
    {
        if (disclaimer == null || !_showDisclaimer)
            return;

        string text = Localization.Get(LocalizationKeys.UiLoadingDisclaimer);
        if (string.IsNullOrEmpty(text) || text == LocalizationKeys.UiLoadingDisclaimer)
        {
            text = "This game is a work of fiction.\n"
                   + "All locations, cards, and packs in the game are entirely imaginary\n"
                   + "and have no connection to any real places or works.";
        }

        disclaimer.text = text;
        disclaimer.ForceMeshUpdate();
    }

    void UpdateLabel()
    {
        if (label == null)
            return;

        string baseText = Localization.Get(LocalizationKeys.UiLoading);
        if (string.IsNullOrEmpty(baseText) || baseText == LocalizationKeys.UiLoading)
            baseText = "Loading";

        int dotFrame = Mathf.FloorToInt((Time.unscaledTime - _showTime) / DotStepDuration) % 4;
        label.text = baseText + new string('.', dotFrame);
    }

    void LateUpdate()
    {
        if (!isActiveAndEnabled)
            return;

        if (spinnerYellow != null)
        {
            float angle = (Time.unscaledTime - _showTime) * SpinnerSpeed;
            spinnerYellow.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        UpdateLabel();
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}

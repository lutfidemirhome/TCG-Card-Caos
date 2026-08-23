using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings overlay for main menu and pause. Author under MainMenuCanvas / InGamePauseCanvas
/// (TCG Card Caos → UI → Add Settings Panel). Runtime only builds if the panel is missing.
/// </summary>
public class SettingsPanelView : MonoBehaviour
{
    static readonly Color BackgroundColor = new Color(0.039f, 0.047f, 0.114f, 1f);
    static readonly Color PanelColor = new Color(0.10f, 0.16f, 0.30f, 0.96f);
    static readonly Color ControlColor = new Color(0.85f, 0.77f, 0.70f, 1f);
    static readonly Color TrackColor = new Color(0.18f, 0.22f, 0.30f, 1f);
    static readonly Color SaveColor = new Color(0.23f, 0.78f, 0.30f, 1f);
    static readonly Color CheckOff = new Color(0.85f, 0.77f, 0.70f, 1f);
    static readonly Color ListColor = new Color(0.85f, 0.77f, 0.70f, 1f);

    const float RowHeight = 54f;
    const float PanelWidth = 760f;
    const float PanelHeight = 820f;

    [SerializeField] GameObject root;

    GameSettings.Snapshot _saved;
    GameSettings.Snapshot _draft;
    readonly List<SettingsDropdown> _dropdowns = new List<SettingsDropdown>(4);
    RectTransform _listOverlay;
    GameLanguage _languageAtOpen;

    SettingsDropdown _languageDropdown;
    SettingsDropdown _resolutionDropdown;
    SettingsDropdown _qualityDropdown;
    Toggle _fullscreenToggle;
    Toggle _invertYToggle;
    Toggle _invertXToggle;
    Slider _fovSlider;
    Slider _sensitivitySlider;
    Slider _masterSlider;
    Slider _musicSlider;
    Slider _sfxSlider;
    TMP_Text _fovValue;
    TMP_Text _sensitivityValue;
    TMP_Text _masterValue;
    TMP_Text _musicValue;
    TMP_Text _sfxValue;
    ResolutionOption[] _resolutions = Array.Empty<ResolutionOption>();
    bool _wired;

    struct ResolutionOption
    {
        public int width;
        public int height;
        public int refreshHz;
        public string label;
    }

    public bool IsOpen => root != null && root.activeSelf;

    void Awake()
    {
        if (transform.Find("Panel") != null)
            BindExisting();
    }

    void OnEnable()
    {
        Localization.LanguageChanged += OnLanguageChanged;
    }

    void OnDisable()
    {
        Localization.LanguageChanged -= OnLanguageChanged;
    }

    void OnLanguageChanged()
    {
        if (IsOpen)
            BindQualityOptions();
    }

    public static SettingsPanelView Ensure(Transform parent)
    {
        if (parent == null)
            return null;

        SettingsPanelView existing = parent.GetComponentInChildren<SettingsPanelView>(true);
        if (existing != null)
        {
            if (existing.transform.Find("Panel") == null)
                existing.Build();
            else
                existing.BindExisting();
            return existing;
        }

        var go = new GameObject("Panel_Settings", typeof(RectTransform), typeof(SettingsPanelView));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var view = go.GetComponent<SettingsPanelView>();
        view.Build();
        view.Hide();
        return view;
    }

    public void BuildInEditor()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        _dropdowns.Clear();
        _wired = false;
        Build();
        root = gameObject;
    }

    void Update()
    {
        if (!IsOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            CancelAndHide();

        if (Input.GetMouseButtonDown(0) && !PointerOverAnyDropdown())
            CloseDropdowns(null);
    }

    public void Show()
    {
        if (transform.Find("Panel") == null)
            Build();
        else
            BindExisting();

        _saved = GameSettings.Current;
        _draft = _saved;
        _languageAtOpen = _saved.language;
        RefreshControls();
        CloseDropdowns(null);

        if (root != null)
            root.SetActive(true);
        else
            gameObject.SetActive(true);

        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        CloseDropdowns(null);
        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    public void Cancel()
    {
        CancelAndHide();
    }

    void CancelAndHide()
    {
        Localization.SetLanguage(_languageAtOpen);
        Hide();
    }

    void SaveAndHide()
    {
        ReadDraftFromControls();
        GameSettings.Save(_draft);
        Hide();
    }

    void Build()
    {
        root = gameObject;

        var background = CreateImage("Background", transform, BackgroundColor);
        Stretch(background.rectTransform);
        background.sprite = null;
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.color = BackgroundColor;

        var panel = CreateImage("Panel", transform, PanelColor);
        SettingsArt.Apply(panel, SettingsArt.Panel, PanelColor);
        panel.preserveAspect = false;
        var panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panelRect.anchoredPosition = new Vector2(0f, -6f);

        var title = CreateLabel("Title", transform, LocalizationKeys.MenuSettings, 58f);
        var titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0f);
        titleRect.anchoredPosition = new Vector2(0f, PanelHeight * 0.5f - 2f);
        titleRect.sizeDelta = new Vector2(520f, 64f);
        title.alignment = TextAlignmentOptions.Center;

        Button back = CreateEscBack(transform);
        back.onClick.AddListener(CancelAndHide);

        Button save = CreateButton("Button_Save", transform, LocalizationKeys.SettingsSave, SettingsArt.ButtonSave, SaveColor);
        var saveRect = save.transform as RectTransform;
        saveRect.anchorMin = new Vector2(0.5f, 0.5f);
        saveRect.anchorMax = new Vector2(0.5f, 0.5f);
        saveRect.pivot = new Vector2(0.5f, 0.5f);
        saveRect.anchoredPosition = new Vector2(0f, -PanelHeight * 0.5f - 6f);
        saveRect.sizeDelta = new Vector2(244f, 72f);
        save.onClick.AddListener(SaveAndHide);

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentGo.transform.SetParent(panel.transform, false);
        var content = (RectTransform)contentGo.transform;
        Stretch(content, 28f, 22f);
        var layout = contentGo.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var overlayGo = new GameObject("DropdownOverlay", typeof(RectTransform));
        overlayGo.transform.SetParent(transform, false);
        _listOverlay = (RectTransform)overlayGo.transform;
        Stretch(_listOverlay);

        _languageDropdown = CreateDropdownRow(content, "Language", LocalizationKeys.SettingsLanguage, SettingsArt.LanguageDropdown, 264f);
        _resolutionDropdown = CreateDropdownRow(content, "Resolution", LocalizationKeys.SettingsResolution, SettingsArt.ResolutionDropdown, 335f);
        _fullscreenToggle = CreateToggleRow(content, "Fullscreen", LocalizationKeys.SettingsFullscreen, value => _draft.fullscreen = value);
        _qualityDropdown = CreateDropdownRow(content, "Quality", LocalizationKeys.SettingsQuality, SettingsArt.QualityDropdown, 240f);
        _fovSlider = CreateSliderRow(content, "Fov", LocalizationKeys.SettingsFov, GameSettings.MinFov, GameSettings.MaxFov, whole: true, out _fovValue, value => _draft.fov = value);
        _sensitivitySlider = CreateSliderRow(content, "Sensitivity", LocalizationKeys.SettingsSensitivity, 0.01f, 1f, whole: false, out _sensitivityValue, value => _draft.sensitivity = value);
        _invertYToggle = CreateToggleRow(content, "InvertY", LocalizationKeys.SettingsInvertY, value => _draft.invertY = value);
        _invertXToggle = CreateToggleRow(content, "InvertX", LocalizationKeys.SettingsInvertX, value => _draft.invertX = value);
        _masterSlider = CreateSliderRow(content, "Master", LocalizationKeys.SettingsMaster, 0f, 100f, whole: true, out _masterValue, value => _draft.masterVolume = value / 100f);
        _musicSlider = CreateSliderRow(content, "Music", LocalizationKeys.SettingsMusic, 0f, 100f, whole: true, out _musicValue, value => _draft.musicVolume = value / 100f);
        _sfxSlider = CreateSliderRow(content, "Sfx", LocalizationKeys.SettingsSfx, 0f, 100f, whole: true, out _sfxValue, value => _draft.sfxVolume = value / 100f);
        _wired = true;
        ApplyControlTextMaterials();
        FillPreviewValues();
    }

    void BindExisting()
    {
        root = gameObject;
        _listOverlay = transform.Find("DropdownOverlay") as RectTransform;
        Transform content = transform.Find("Panel/Content");
        if (content == null)
            content = transform.Find("Panel/Rows/Content");
        if (content == null)
            return;

        _dropdowns.Clear();
        _languageDropdown = BindDropdown(content, "Language");
        _resolutionDropdown = BindDropdown(content, "Resolution");
        _qualityDropdown = BindDropdown(content, "Quality");
        _fullscreenToggle = BindToggle(content, "Fullscreen", value => _draft.fullscreen = value);
        _invertYToggle = BindToggle(content, "InvertY", value => _draft.invertY = value);
        _invertXToggle = BindToggle(content, "InvertX", value => _draft.invertX = value);
        _fovSlider = BindSlider(content, "Fov", "{0:0}", value => _draft.fov = value, out _fovValue);
        _sensitivitySlider = BindSlider(content, "Sensitivity", "{0:0.00}", value => _draft.sensitivity = value, out _sensitivityValue);
        _masterSlider = BindSlider(content, "Master", "{0:0}", value => _draft.masterVolume = value / 100f, out _masterValue);
        _musicSlider = BindSlider(content, "Music", "{0:0}", value => _draft.musicVolume = value / 100f, out _musicValue);
        _sfxSlider = BindSlider(content, "Sfx", "{0:0}", value => _draft.sfxVolume = value / 100f, out _sfxValue);

        if (_wired)
            return;

        Button back = transform.Find("Button_Back")?.GetComponent<Button>();
        if (back != null)
        {
            back.onClick.RemoveListener(CancelAndHide);
            back.onClick.AddListener(CancelAndHide);
        }

        Button save = transform.Find("Button_Save")?.GetComponent<Button>();
        if (save != null)
        {
            save.onClick.RemoveListener(SaveAndHide);
            save.onClick.AddListener(SaveAndHide);
        }

        _wired = true;
        ApplyControlTextMaterials();
    }

    public void FillPreviewValues()
    {
        if (transform.Find("Panel") == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Localization.ReloadTable();
#endif

        BindExisting();
        _draft = GameSettings.Current;
        RefreshControls();
    }

    public void ApplyControlTextMaterials()
    {
        ApplyControlAt("Panel/Content/Row_Language/Dropdown/Value");
        ApplyControlAt("Panel/Content/Row_Resolution/Dropdown/Value");
        ApplyControlAt("Panel/Content/Row_Quality/Dropdown/Value");
        ApplyControlAt("Panel/Content/Row_Fov/ValueBox/Value");
        ApplyControlAt("Panel/Content/Row_Sensitivity/ValueBox/Value");
        ApplyControlAt("Panel/Content/Row_Master/ValueBox/Value");
        ApplyControlAt("Panel/Content/Row_Music/ValueBox/Value");
        ApplyControlAt("Panel/Content/Row_Sfx/ValueBox/Value");
    }

    void ApplyControlAt(string path)
    {
        Transform found = transform.Find(path);
        if (found == null)
            return;

        UiMenuFont.ApplyControl(found.GetComponent<TMP_Text>());
    }

    SettingsDropdown BindDropdown(Transform content, string name)
    {
        Transform header = content.Find("Row_" + name + "/Dropdown");
        if (header == null)
            return null;

        var dropdown = header.GetComponent<SettingsDropdown>();
        if (dropdown == null)
            dropdown = header.gameObject.AddComponent<SettingsDropdown>();

        Button button = header.GetComponent<Button>();
        TMP_Text label = header.Find("Value")?.GetComponent<TMP_Text>();
        RectTransform list = _listOverlay != null
            ? _listOverlay.Find(name + "List") as RectTransform
            : null;
        RectTransform listContent = list != null ? list.Find("Content") as RectTransform : null;
        dropdown.Bind(button, label, list, listContent, CloseDropdowns);
        _dropdowns.Add(dropdown);
        return dropdown;
    }

    static Toggle BindToggle(Transform content, string name, Action<bool> changed)
    {
        Transform toggleTransform = content.Find("Row_" + name + "/Toggle");
        Toggle toggle = toggleTransform != null ? toggleTransform.GetComponent<Toggle>() : null;
        if (toggle == null)
            return null;

        toggle.onValueChanged.RemoveAllListeners();
        toggle.onValueChanged.AddListener(value => changed?.Invoke(value));
        return toggle;
    }

    static Slider BindSlider(
        Transform content,
        string name,
        string format,
        Action<float> changed,
        out TMP_Text valueLabel)
    {
        Transform row = content.Find("Row_" + name);
        Slider slider = row != null ? row.Find("Slider")?.GetComponent<Slider>() : null;
        valueLabel = row != null ? row.Find("ValueBox/Value")?.GetComponent<TMP_Text>() : null;
        if (slider == null)
            return null;

        TMP_Text captured = valueLabel;
        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(value =>
        {
            if (captured != null)
                captured.text = string.Format(format, value);
            changed?.Invoke(value);
        });
        return slider;
    }

    void RefreshControls()
    {
        BindLanguageOptions();
        BindResolutionOptions();
        BindQualityOptions();

        if (_fullscreenToggle != null)
            _fullscreenToggle.SetIsOnWithoutNotify(_draft.fullscreen);
        if (_invertYToggle != null)
            _invertYToggle.SetIsOnWithoutNotify(_draft.invertY);
        if (_invertXToggle != null)
            _invertXToggle.SetIsOnWithoutNotify(_draft.invertX);

        SetSlider(_fovSlider, _fovValue, _draft.fov, "{0:0}");
        SetSlider(_sensitivitySlider, _sensitivityValue, _draft.sensitivity, "{0:0.00}");
        SetSlider(_masterSlider, _masterValue, _draft.masterVolume * 100f, "{0:0}");
        SetSlider(_musicSlider, _musicValue, _draft.musicVolume * 100f, "{0:0}");
        SetSlider(_sfxSlider, _sfxValue, _draft.sfxVolume * 100f, "{0:0}");
    }

    void ReadDraftFromControls()
    {
        if (_languageDropdown != null)
            _draft.language = (GameLanguage)_languageDropdown.SelectedIndex;
        if (_resolutionDropdown != null && _resolutions.Length > 0)
        {
            ResolutionOption option = _resolutions[Mathf.Clamp(_resolutionDropdown.SelectedIndex, 0, _resolutions.Length - 1)];
            _draft.width = option.width;
            _draft.height = option.height;
            _draft.refreshHz = option.refreshHz;
        }

        if (_qualityDropdown != null)
            _draft.quality = (GameSettings.QualityTier)_qualityDropdown.SelectedIndex;
        if (_fullscreenToggle != null)
            _draft.fullscreen = _fullscreenToggle.isOn;
        if (_invertYToggle != null)
            _draft.invertY = _invertYToggle.isOn;
        if (_invertXToggle != null)
            _draft.invertX = _invertXToggle.isOn;
        if (_fovSlider != null)
            _draft.fov = _fovSlider.value;
        if (_sensitivitySlider != null)
            _draft.sensitivity = _sensitivitySlider.value;
        if (_masterSlider != null)
            _draft.masterVolume = _masterSlider.value / 100f;
        if (_musicSlider != null)
            _draft.musicVolume = _musicSlider.value / 100f;
        if (_sfxSlider != null)
            _draft.sfxVolume = _sfxSlider.value / 100f;
    }

    void BindLanguageOptions()
    {
        var names = new string[GameLanguages.Count];
        for (int i = 0; i < names.Length; i++)
            names[i] = GameLanguages.GetNativeName((GameLanguage)i);

        _languageDropdown?.SetOptions(names, (int)_draft.language, index =>
        {
            _draft.language = (GameLanguage)index;
            Localization.SetLanguage(_draft.language);
        });
    }

    void BindQualityOptions()
    {
        string[] names =
        {
            Localization.Get(LocalizationKeys.SettingsQualityLow),
            Localization.Get(LocalizationKeys.SettingsQualityMedium),
            Localization.Get(LocalizationKeys.SettingsQualityHigh)
        };
        _qualityDropdown?.SetOptions(names, (int)_draft.quality, index =>
        {
            _draft.quality = (GameSettings.QualityTier)index;
        });
    }

    void BindResolutionOptions()
    {
        var unique = new List<ResolutionOption>(32);
        Resolution[] available = Screen.resolutions;
        for (int i = 0; i < available.Length; i++)
        {
            Resolution res = available[i];
            int hz = Mathf.Max(1, Mathf.RoundToInt((float)res.refreshRateRatio.value));
            AddResolution(unique, res.width, res.height, hz);
        }

        AddResolution(unique, _draft.width, _draft.height, _draft.refreshHz);
        unique.Sort((a, b) =>
        {
            int width = a.width.CompareTo(b.width);
            if (width != 0)
                return width;
            int height = a.height.CompareTo(b.height);
            return height != 0 ? height : a.refreshHz.CompareTo(b.refreshHz);
        });

        _resolutions = unique.ToArray();
        var labels = new string[_resolutions.Length];
        int selected = 0;
        for (int i = 0; i < _resolutions.Length; i++)
        {
            labels[i] = _resolutions[i].label;
            if (_resolutions[i].width == _draft.width
                && _resolutions[i].height == _draft.height
                && _resolutions[i].refreshHz == _draft.refreshHz)
                selected = i;
        }

        _resolutionDropdown?.SetOptions(labels, selected, index =>
        {
            if (index < 0 || index >= _resolutions.Length)
                return;
            _draft.width = _resolutions[index].width;
            _draft.height = _resolutions[index].height;
            _draft.refreshHz = _resolutions[index].refreshHz;
        });
    }

    static void AddResolution(List<ResolutionOption> list, int width, int height, int refreshHz)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].width == width && list[i].height == height && list[i].refreshHz == refreshHz)
                return;
        }

        list.Add(new ResolutionOption
        {
            width = width,
            height = height,
            refreshHz = refreshHz,
            label = GameSettings.FormatResolution(width, height, refreshHz)
        });
    }

    SettingsDropdown CreateDropdownRow(Transform parent, string name, string labelKey, Sprite sprite, float width)
    {
        RectTransform row = CreateRow(parent, name);
        CreateRowLabel(row, labelKey);

        var headerGo = new GameObject("Dropdown", typeof(RectTransform), typeof(Image), typeof(Button));
        headerGo.transform.SetParent(row, false);
        PlaceControl(headerGo.transform as RectTransform, width, 52f);
        var headerImage = headerGo.GetComponent<Image>();
        SettingsArt.Apply(headerImage, sprite, ControlColor);
        headerImage.preserveAspect = false;
        var headerButton = headerGo.GetComponent<Button>();
        headerButton.targetGraphic = headerImage;

        var labelGo = new GameObject("Value", typeof(RectTransform));
        labelGo.transform.SetParent(headerGo.transform, false);
        var labelRect = (RectTransform)labelGo.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(16f, 4f);
        labelRect.offsetMax = new Vector2(-40f, -4f);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Left;
        label.fontSize = 26f;
        label.color = Color.black;
        label.raycastTarget = false;
        UiMenuFont.ApplyControl(label);

        var listGo = new GameObject(name + "List", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(Mask));
        listGo.transform.SetParent(_listOverlay, false);
        var listRect = (RectTransform)listGo.transform;
        var listImage = listGo.GetComponent<Image>();
        listImage.sprite = null;
        listImage.color = ListColor;
        listImage.preserveAspect = false;
        listGo.GetComponent<Mask>().showMaskGraphic = true;
        var scroll = listGo.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(listGo.transform, false);
        var content = (RectTransform)contentGo.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var layout = contentGo.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 2f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;

        var dropdown = headerGo.AddComponent<SettingsDropdown>();
        dropdown.Bind(headerButton, label, listRect, content, CloseDropdowns);
        _dropdowns.Add(dropdown);
        return dropdown;
    }

    Toggle CreateToggleRow(Transform parent, string name, string labelKey, Action<bool> changed)
    {
        RectTransform row = CreateRow(parent, name);
        CreateRowLabel(row, labelKey);

        var toggleGo = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
        toggleGo.transform.SetParent(row, false);
        PlaceControl(toggleGo.transform as RectTransform, 44f, 44f);
        var box = toggleGo.GetComponent<Image>();
        SettingsArt.Apply(box, SettingsArt.CheckboxOff, CheckOff);

        var checkGo = new GameObject("Check", typeof(RectTransform), typeof(Image));
        checkGo.transform.SetParent(toggleGo.transform, false);
        var checkRect = (RectTransform)checkGo.transform;
        Stretch(checkRect, 8f, 8f);
        var check = checkGo.GetComponent<Image>();
        SettingsArt.Apply(check, SettingsArt.CheckboxOn, Color.white);
        check.preserveAspect = true;

        var toggle = toggleGo.GetComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = check;
        toggle.onValueChanged.AddListener(value => changed?.Invoke(value));
        return toggle;
    }

    Slider CreateSliderRow(
        Transform parent,
        string name,
        string labelKey,
        float min,
        float max,
        bool whole,
        out TMP_Text valueLabel,
        Action<float> changed)
    {
        RectTransform row = CreateRow(parent, name);
        CreateRowLabel(row, labelKey);

        var valueGo = new GameObject("ValueBox", typeof(RectTransform), typeof(Image));
        valueGo.transform.SetParent(row, false);
        var valueRect = (RectTransform)valueGo.transform;
        valueRect.anchorMin = new Vector2(1f, 0.5f);
        valueRect.anchorMax = new Vector2(1f, 0.5f);
        valueRect.pivot = new Vector2(1f, 0.5f);
        valueRect.anchoredPosition = new Vector2(-286f, 0f);
        valueRect.sizeDelta = new Vector2(56f, 40f);
        SettingsArt.Apply(valueGo.GetComponent<Image>(), SettingsArt.ValueBox, ControlColor);

        var valueTextGo = new GameObject("Value", typeof(RectTransform));
        valueTextGo.transform.SetParent(valueGo.transform, false);
        Stretch((RectTransform)valueTextGo.transform, 4f, 2f);
        valueLabel = valueTextGo.AddComponent<TextMeshProUGUI>();
        valueLabel.alignment = TextAlignmentOptions.Center;
        valueLabel.fontSize = 24f;
        valueLabel.color = Color.black;
        valueLabel.raycastTarget = false;
        UiMenuFont.ApplyControl(valueLabel);

        var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(row, false);
        PlaceControl(sliderGo.transform as RectTransform, 262f, 40f);

        var trackGo = new GameObject("Track", typeof(RectTransform), typeof(Image));
        trackGo.transform.SetParent(sliderGo.transform, false);
        Stretch((RectTransform)trackGo.transform);
        var trackImage = trackGo.GetComponent<Image>();
        SettingsArt.Apply(trackImage, SettingsArt.SliderTrack, TrackColor);
        trackImage.preserveAspect = false;

        var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGo.transform.SetParent(sliderGo.transform, false);
        Stretch((RectTransform)fillAreaGo.transform, 8f, 8f);

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        var fillRect = (RectTransform)fillGo.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fillGo.GetComponent<Image>();
        SettingsArt.Apply(fillImage, SettingsArt.SliderFill, new Color(0.75f, 0.78f, 0.82f, 1f));
        fillImage.type = Image.Type.Tiled;
        fillImage.preserveAspect = false;

        var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGo.transform.SetParent(sliderGo.transform, false);
        Stretch((RectTransform)handleAreaGo.transform);

        var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(handleAreaGo.transform, false);
        var handleRect = (RectTransform)handleGo.transform;
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(49f, 49f);
        var handleImage = handleGo.GetComponent<Image>();
        SettingsArt.Apply(handleImage, SettingsArt.SliderHandle, Color.white);
        handleImage.preserveAspect = true;

        var slider = sliderGo.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        slider.targetGraphic = handleImage;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        TMP_Text captured = valueLabel;
        string format = whole ? "{0:0}" : "{0:0.00}";
        slider.onValueChanged.AddListener(value =>
        {
            captured.text = string.Format(format, value);
            changed?.Invoke(value);
        });
        return slider;
    }

    static void SetSlider(Slider slider, TMP_Text value, float number, string format)
    {
        if (slider == null)
            return;
        slider.SetValueWithoutNotify(number);
        if (value != null)
            value.text = string.Format(format, number);
    }

    RectTransform CreateRow(Transform parent, string name)
    {
        var rowGo = new GameObject("Row_" + name, typeof(RectTransform), typeof(LayoutElement));
        rowGo.transform.SetParent(parent, false);
        var row = (RectTransform)rowGo.transform;
        rowGo.GetComponent<LayoutElement>().preferredHeight = RowHeight;
        rowGo.GetComponent<LayoutElement>().minHeight = RowHeight;
        return row;
    }

    static void CreateRowLabel(Transform row, string key)
    {
        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(row, false);
        var rect = (RectTransform)labelGo.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0.48f, 1f);
        rect.offsetMin = new Vector2(12f, 4f);
        rect.offsetMax = new Vector2(-8f, -4f);
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.fontSize = 26f;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        UiMenuFont.Apply(tmp);
        var localized = labelGo.AddComponent<LocalizedText>();
        localized.SetKey(key);
    }

    static void PlaceControl(RectTransform rect, float width, float height)
    {
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-18f, 0f);
        rect.sizeDelta = new Vector2(width, height);
    }

    Button CreateEscBack(Transform parent)
    {
        var go = new GameObject("Button_Back", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(28f, -24f);
        rect.sizeDelta = new Vector2(220f, 72f);
        var image = go.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.01f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        var iconGo = new GameObject("EscIcon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        var iconRect = (RectTransform)iconGo.transform;
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(64f, 64f);
        var icon = iconGo.GetComponent<Image>();
        SettingsArt.Apply(icon, SettingsArt.EscIcon, ControlColor);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var labelRect = (RectTransform)labelGo.transform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(76f, 8f);
        labelRect.offsetMax = new Vector2(-8f, -8f);
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.fontSize = 36f;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        UiMenuFont.Apply(tmp);
        var localized = labelGo.AddComponent<LocalizedText>();
        localized.SetKey(LocalizationKeys.PauseBack);
        return button;
    }

    Button CreateButton(string name, Transform parent, string key, Sprite sprite, Color fallback)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        SettingsArt.Apply(image, sprite, fallback);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        Stretch((RectTransform)labelGo.transform, 12f, 8f);
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 32f;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        UiMenuFont.Apply(tmp);
        var localized = labelGo.AddComponent<LocalizedText>();
        localized.SetKey(key);
        return button;
    }

    static TextMeshProUGUI CreateLabel(string name, Transform parent, string key, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        UiMenuFont.Apply(tmp);
        var localized = go.AddComponent<LocalizedText>();
        localized.SetKey(key);
        return tmp;
    }

    static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    static void Stretch(RectTransform rect, float insetX = 0f, float insetY = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(insetX, insetY);
        rect.offsetMax = new Vector2(-insetX, -insetY);
    }

    void CloseDropdowns(SettingsDropdown except)
    {
        for (int i = 0; i < _dropdowns.Count; i++)
        {
            if (_dropdowns[i] == null || _dropdowns[i] == except)
                continue;
            _dropdowns[i].Close();
        }
    }

    bool PointerOverAnyDropdown()
    {
        for (int i = 0; i < _dropdowns.Count; i++)
        {
            SettingsDropdown dropdown = _dropdowns[i];
            if (dropdown == null || !dropdown.IsOpen)
                continue;

            var list = dropdown.transform;
            if (RectTransformUtility.RectangleContainsScreenPoint(dropdown.transform as RectTransform, Input.mousePosition, null))
                return true;
        }

        if (_listOverlay == null)
            return false;

        for (int i = 0; i < _listOverlay.childCount; i++)
        {
            var child = _listOverlay.GetChild(i) as RectTransform;
            if (child != null && child.gameObject.activeSelf
                && RectTransformUtility.RectangleContainsScreenPoint(child, Input.mousePosition, null))
                return true;
        }

        return false;
    }
}

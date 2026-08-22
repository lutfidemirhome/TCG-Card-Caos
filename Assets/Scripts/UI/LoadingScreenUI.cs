using TMPro;
using UnityEngine;

/// <summary>
/// One loading overlay for every path: menu Continue/New Game and in-game Load Save.
/// Runtime always instantiates Resources/UI/LoadingScreen so both look the same.
/// Scene copies in MenuScene / MainScene are for editing only.
/// Only SpinnerYellow rotates.
/// </summary>
public class LoadingScreenUI : MonoBehaviour
{
    const float SpinnerSpeed = 220f;
    const float DotStepDuration = 0.35f;
    const int SortingOrder = 1000;
    const string PrefabResourcePath = "UI/LoadingScreen";

    static readonly Color LabelColor = new Color(0.92f, 0.92f, 0.92f, 1f);

    static LoadingScreenUI _instance;

    [SerializeField] GameObject root;
    [SerializeField] RectTransform spinnerYellow;
    [SerializeField] TMP_Text label;
    [SerializeField] bool editorPreview;

    float _showTime;

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
            instance.SetActive(false);
            return _instance;
        }

        LoadingScreenUI existing = FindFirstObjectByType<LoadingScreenUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            _instance.editorPreview = false;
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
        if (root != null)
            root.SetActive(false);
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
    }

    public void Show()
    {
        Bind();
        _showTime = Time.unscaledTime;

        gameObject.SetActive(true);
        if (root != null)
            root.SetActive(true);

        transform.SetAsLastSibling();

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = SortingOrder;

        PrepareLabel();
        UpdateLabel();
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);

        gameObject.SetActive(false);
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

        TMP_FontAsset font = UiMenuFont.Font;
        if (font != null && font.material != null)
        {
            label.font = font;
            label.fontSharedMaterial = font.material;
        }

        label.ForceMeshUpdate();
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

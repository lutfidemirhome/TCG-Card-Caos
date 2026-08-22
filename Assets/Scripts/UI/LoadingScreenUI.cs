using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen loading overlay. Background is a placeholder until art is swapped in.
/// </summary>
public class LoadingScreenUI : MonoBehaviour
{
    static LoadingScreenUI _instance;

    static readonly Color BackgroundColor = new Color(0.14f, 0.14f, 0.14f, 1f);
    static readonly Color SpinnerColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    static readonly Color LabelColor = new Color(0.88f, 0.88f, 0.88f, 1f);

    const float SpinnerSize = 56f;
    const float LabelSpacing = 22f;
    const float BottomOffset = 96f;
    const float SpinnerSpeed = 220f;
    const float DotStepDuration = 0.35f;

    RectTransform _spinnerRect;
    Text _label;
    float _showTime;

    public static LoadingScreenUI Instance => _instance;

    public static LoadingScreenUI Ensure()
    {
        if (_instance != null)
            return _instance;

        var root = new GameObject(nameof(LoadingScreenUI));
        DontDestroyOnLoad(root);
        _instance = root.AddComponent<LoadingScreenUI>();
        _instance.BuildUi();
        root.SetActive(false);
        return _instance;
    }

    void BuildUi()
    {
        Canvas canvas = RuntimeOverlayCanvasFactory.Create(transform, "LoadingCanvas", sortingOrder: 1000, matchWidthOrHeight: 0.5f);

        var backgroundGo = new GameObject("Background");
        backgroundGo.transform.SetParent(canvas.transform, false);

        var background = backgroundGo.AddComponent<Image>();
        background.color = BackgroundColor;
        background.raycastTarget = true;

        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        var stackGo = new GameObject("LoadingStack");
        stackGo.transform.SetParent(canvas.transform, false);

        RectTransform stackRect = stackGo.AddComponent<RectTransform>();
        stackRect.anchorMin = new Vector2(0.5f, 0f);
        stackRect.anchorMax = new Vector2(0.5f, 0f);
        stackRect.pivot = new Vector2(0.5f, 0f);
        stackRect.anchoredPosition = new Vector2(0f, BottomOffset);
        stackRect.sizeDelta = new Vector2(320f, SpinnerSize + LabelSpacing + 40f);

        var layout = stackGo.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = LabelSpacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var spinnerGo = new GameObject("Spinner");
        spinnerGo.transform.SetParent(stackGo.transform, false);

        var spinner = spinnerGo.AddComponent<Image>();
        spinner.sprite = CreateSpinnerSprite(96);
        spinner.color = SpinnerColor;
        spinner.raycastTarget = false;

        _spinnerRect = spinner.rectTransform;
        _spinnerRect.sizeDelta = new Vector2(SpinnerSize, SpinnerSize);

        var spinnerLayout = spinnerGo.AddComponent<LayoutElement>();
        spinnerLayout.preferredWidth = SpinnerSize;
        spinnerLayout.preferredHeight = SpinnerSize;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(stackGo.transform, false);

        _label = labelGo.AddComponent<Text>();
        _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _label.fontSize = 28;
        _label.fontStyle = FontStyle.Bold;
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = LabelColor;
        _label.raycastTarget = false;

        var labelLayout = labelGo.AddComponent<LayoutElement>();
        labelLayout.preferredHeight = 36f;

        RectTransform labelRect = _label.rectTransform;
        labelRect.sizeDelta = new Vector2(320f, 36f);

        UpdateLabel();
    }

    public void Show()
    {
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null)
            canvas.sortingOrder = 1000;

        _showTime = Time.unscaledTime;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        UpdateLabel();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (!gameObject.activeSelf)
            return;

        if (_spinnerRect != null)
        {
            float angle = (Time.unscaledTime - _showTime) * SpinnerSpeed;
            _spinnerRect.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        UpdateLabel();
    }

    void UpdateLabel()
    {
        if (_label == null)
            return;

        int dotFrame = Mathf.FloorToInt((Time.unscaledTime - _showTime) / DotStepDuration) % 4;
        _label.text = "Loading" + new string('.', dotFrame);
    }

    static Sprite CreateSpinnerSprite(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        float center = size * 0.5f;
        float outerRadius = center - 2f;
        float innerRadius = outerRadius * 0.68f;
        float arcStart = 0f;
        float arcEnd = 270f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - center;
                float dy = y + 0.5f - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                if (distance > outerRadius || distance < innerRadius)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (angle < 0f)
                    angle += 360f;

                bool inArc = arcStart < arcEnd
                    ? angle >= arcStart && angle <= arcEnd
                    : angle >= arcStart || angle <= arcEnd;

                texture.SetPixel(x, y, inArc ? Color.white : Color.clear);
            }
        }

        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}

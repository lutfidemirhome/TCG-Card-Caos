using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Temporary playtest HUD: top-left round pause button + elapsed timer.
/// Remove when timing tests are done.
/// </summary>
public sealed class TempPlaytestPauseUI : MonoBehaviour
{
    const float ButtonSize = 44f;
    const float Margin = 24f;
    const float TimerGap = 12f;

    static readonly Color ButtonColor = new Color(0.12f, 0.12f, 0.14f, 0.88f);
    static readonly Color ButtonHoverColor = new Color(0.22f, 0.22f, 0.26f, 0.95f);
    static readonly Color TimerColor = new Color(1f, 1f, 1f, 0.95f);

    float _elapsedSeconds;
    bool _paused;
    Text _timerText;
    Text _pauseIconText;

    public static TempPlaytestPauseUI EnsureOn(Camera camera)
    {
        if (camera == null)
            return null;

        var existing = camera.GetComponent<TempPlaytestPauseUI>();
        if (existing != null)
            return existing;

        return camera.gameObject.AddComponent<TempPlaytestPauseUI>();
    }

    void Awake()
    {
        EnsureEventSystem();
        BuildUI();
    }

    void Update()
    {
        if (!_paused)
            _elapsedSeconds += Time.deltaTime;

        if (_timerText != null)
            _timerText.text = FormatElapsed(_elapsedSeconds);
    }

    void OnDestroy()
    {
        if (_paused)
            Time.timeScale = 1f;
    }

    public static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        var eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<StandaloneInputModule>();
    }

    void BuildUI()
    {
        Canvas canvas = RuntimeOverlayCanvasFactory.Create(
            transform,
            "TempPlaytestPauseCanvas",
            sortingOrder: 150,
            matchWidthOrHeight: 0.5f);

        var rootGo = new GameObject("PlaytestHudRoot");
        rootGo.transform.SetParent(canvas.transform, false);

        RectTransform root = rootGo.AddComponent<RectTransform>();
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(Margin, -Margin);
        root.sizeDelta = new Vector2(ButtonSize + TimerGap + 180f, ButtonSize);

        BuildPauseButton(root);
        BuildTimerLabel(root);
    }

    void BuildPauseButton(RectTransform root)
    {
        var buttonGo = new GameObject("PauseButton");
        buttonGo.transform.SetParent(root, false);

        RectTransform rect = buttonGo.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);

        var image = buttonGo.AddComponent<Image>();
        image.sprite = CreateCircleSprite(64, Color.white);
        image.type = Image.Type.Simple;
        image.color = ButtonColor;

        var button = buttonGo.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = ButtonHoverColor;
        colors.pressedColor = new Color(0.08f, 0.08f, 0.1f, 1f);
        colors.selectedColor = ButtonHoverColor;
        button.colors = colors;
        button.onClick.AddListener(TogglePause);

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(buttonGo.transform, false);
        _pauseIconText = iconGo.AddComponent<Text>();
        _pauseIconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _pauseIconText.text = "||";
        _pauseIconText.alignment = TextAnchor.MiddleCenter;
        _pauseIconText.color = Color.white;
        _pauseIconText.raycastTarget = false;
        _pauseIconText.resizeTextForBestFit = true;
        _pauseIconText.resizeTextMinSize = 10;
        _pauseIconText.resizeTextMaxSize = 22;

        RectTransform iconRect = _pauseIconText.rectTransform;
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
    }

    void BuildTimerLabel(RectTransform root)
    {
        var timerGo = new GameObject("Timer");
        timerGo.transform.SetParent(root, false);

        _timerText = timerGo.AddComponent<Text>();
        _timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _timerText.text = "00:00";
        _timerText.alignment = TextAnchor.MiddleLeft;
        _timerText.color = TimerColor;
        _timerText.raycastTarget = false;
        _timerText.fontSize = 28;
        _timerText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _timerText.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = _timerText.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(ButtonSize + TimerGap, 0f);
        rect.sizeDelta = new Vector2(180f, ButtonSize);
    }

    void TogglePause()
    {
        _paused = !_paused;
        Time.timeScale = _paused ? 0f : 1f;

        if (_paused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (_pauseIconText != null)
            _pauseIconText.text = _paused ? ">" : "||";
    }

    static string FormatElapsed(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int remainder = Mathf.FloorToInt(seconds % 60f);
        return $"{minutes:00}:{remainder:00}";
    }

    static Sprite CreateCircleSprite(int textureSize, Color fillColor)
    {
        var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        float center = textureSize * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float dx = x + 0.5f - center;
                float dy = y + 0.5f - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                texture.SetPixel(x, y, distance <= radius ? fillColor : Color.clear);
            }
        }

        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            100f);
    }
}

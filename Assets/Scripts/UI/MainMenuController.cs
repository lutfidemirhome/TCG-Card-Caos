using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Runtime-built main menu. Background is a placeholder until art is swapped in.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    static readonly Color BackgroundColor = new Color(0.18f, 0.42f, 0.78f, 1f);
    static readonly Color BorderColor = new Color(0.04f, 0.04f, 0.04f, 1f);
    static readonly Color TextColor = Color.white;
    static readonly Color TextShadowColor = new Color(0f, 0f, 0f, 0.65f);

    const float ButtonWidth = 520f;
    const float ButtonHeight = 74f;
    const float ButtonSpacing = 18f;
    const int BorderThickness = 4;
    const int CornerRadius = 14;

    struct MenuButtonStyle
    {
        public Color Top;
        public Color Bottom;
    }

    static readonly MenuButtonStyle NewGameStyle = new MenuButtonStyle
    {
        Top = new Color(1f, 0.72f, 0.18f, 1f),
        Bottom = new Color(0.92f, 0.45f, 0.08f, 1f)
    };

    static readonly MenuButtonStyle LoadGameStyle = new MenuButtonStyle
    {
        Top = new Color(0.62f, 0.96f, 0.18f, 1f),
        Bottom = new Color(0.28f, 0.72f, 0.12f, 1f)
    };

    static readonly MenuButtonStyle SettingsStyle = new MenuButtonStyle
    {
        Top = new Color(0.28f, 0.88f, 0.98f, 1f),
        Bottom = new Color(0.08f, 0.52f, 0.92f, 1f)
    };

    static readonly MenuButtonStyle QuitStyle = new MenuButtonStyle
    {
        Top = new Color(1f, 0.48f, 0.28f, 1f),
        Bottom = new Color(0.82f, 0.16f, 0.12f, 1f)
    };

    static Sprite _roundedSprite;

    void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EnsureMenuCamera();
        EnsureEventSystem();
        BuildMenu();
        GameAssetPrewarm.Start(this);
    }

    static void EnsureMenuCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.depth = -1;
        }
        else
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
        }

        if (camera.GetComponent<AudioListener>() == null)
            camera.gameObject.AddComponent<AudioListener>();
    }

    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        var eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<StandaloneInputModule>();
    }

    void BuildMenu()
    {
        Canvas canvas = RuntimeOverlayCanvasFactory.Create(transform, "MainMenuCanvas", sortingOrder: 0, matchWidthOrHeight: 0.5f);

        CreateBackground(canvas.transform);
        CreateButtonStack(canvas.transform);
    }

    static void CreateBackground(Transform parent)
    {
        var backgroundGo = new GameObject("Background");
        backgroundGo.transform.SetParent(parent, false);

        var background = backgroundGo.AddComponent<Image>();
        background.color = BackgroundColor;
        background.raycastTarget = true;

        RectTransform rect = background.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void CreateButtonStack(Transform parent)
    {
        var stackGo = new GameObject("MenuButtons");
        stackGo.transform.SetParent(parent, false);

        var stackRect = stackGo.AddComponent<RectTransform>();
        stackRect.anchorMin = new Vector2(0.5f, 0.5f);
        stackRect.anchorMax = new Vector2(0.5f, 0.5f);
        stackRect.pivot = new Vector2(0.5f, 0.5f);
        stackRect.anchoredPosition = new Vector2(0f, -20f);
        stackRect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight * 4f + ButtonSpacing * 3f);

        var layout = stackGo.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = ButtonSpacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        CreateMenuButton(stackGo.transform, "New Game", NewGameStyle, OnNewGameClicked);
        CreateMenuButton(stackGo.transform, "Load Game", LoadGameStyle, OnLoadGameClicked);
        CreateMenuButton(stackGo.transform, "Settings", SettingsStyle, OnSettingsClicked);
        CreateMenuButton(stackGo.transform, "Quit", QuitStyle, OnQuitClicked);
    }

    void CreateMenuButton(Transform parent, string label, MenuButtonStyle style, UnityEngine.Events.UnityAction onClick)
    {
        var buttonGo = new GameObject(label + "Button");
        buttonGo.transform.SetParent(parent, false);

        var layoutElement = buttonGo.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = ButtonWidth;
        layoutElement.preferredHeight = ButtonHeight;

        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

        var border = buttonGo.AddComponent<Image>();
        border.sprite = GetRoundedSprite();
        border.type = Image.Type.Sliced;
        border.color = BorderColor;
        border.raycastTarget = true;

        var button = buttonGo.AddComponent<Button>();
        button.targetGraphic = border;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.65f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(buttonGo.transform, false);

        var fill = fillGo.AddComponent<Image>();
        fill.sprite = CreateGradientSprite(style.Top, style.Bottom);
        fill.type = Image.Type.Sliced;
        fill.raycastTarget = false;

        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(BorderThickness, BorderThickness);
        fillRect.offsetMax = new Vector2(-BorderThickness, -BorderThickness);

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(buttonGo.transform, false);

        var text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 34;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = TextColor;
        text.text = label;
        text.raycastTarget = false;

        var shadow = textGo.AddComponent<Shadow>();
        shadow.effectColor = TextShadowColor;
        shadow.effectDistance = new Vector2(2f, -2f);

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    static Sprite GetRoundedSprite()
    {
        if (_roundedSprite != null)
            return _roundedSprite;

        _roundedSprite = CreateSolidRoundedSprite(BorderColor, CornerRadius + BorderThickness);
        return _roundedSprite;
    }

    static Sprite CreateSolidRoundedSprite(Color color, int radius)
    {
        const int width = 64;
        const int height = 64;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float radiusPixels = radius * (width / (float)(CornerRadius + BorderThickness));

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, IsInsideRoundedRect(x + 0.5f, y + 0.5f, width, height, radiusPixels)
                    ? color
                    : Color.clear);
            }
        }

        texture.Apply();
        return CreateSlicedSprite(texture, radius);
    }

    static Sprite CreateGradientSprite(Color top, Color bottom)
    {
        const int width = 64;
        const int height = 64;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float radiusPixels = CornerRadius * (width / (float)CornerRadius);

        for (int y = 0; y < height; y++)
        {
            float t = y / (height - 1f);
            Color rowColor = Color.Lerp(bottom, top, t);

            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, IsInsideRoundedRect(x + 0.5f, y + 0.5f, width, height, radiusPixels)
                    ? rowColor
                    : Color.clear);
            }
        }

        texture.Apply();
        return CreateSlicedSprite(texture, CornerRadius);
    }

    static Sprite CreateSlicedSprite(Texture2D texture, int radius)
    {
        float border = radius + 1f;
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
    }

    static bool IsInsideRoundedRect(float x, float y, float width, float height, float radius)
    {
        radius = Mathf.Min(radius, width * 0.5f - 0.5f, height * 0.5f - 0.5f);

        if (x >= radius && x <= width - radius)
            return y >= 0f && y <= height;

        if (y >= radius && y <= height - radius)
            return x >= 0f && x <= width;

        float cx = x < radius ? radius : width - radius;
        float cy = y < radius ? radius : height - radius;
        float dx = x - cx;
        float dy = y - cy;
        return dx * dx + dy * dy <= radius * radius;
    }

    static void OnNewGameClicked()
    {
        GameSceneLoader.LoadGame();
    }

    static void OnLoadGameClicked()
    {
        Debug.Log("[MainMenu] Load Game is not implemented yet.");
    }

    static void OnSettingsClicked()
    {
        Debug.Log("[MainMenu] Settings is not implemented yet.");
    }

    static void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

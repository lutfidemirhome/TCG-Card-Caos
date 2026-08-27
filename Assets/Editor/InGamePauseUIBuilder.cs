using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the authored in-game pause overlay in MainScene.
/// Menu: TCG Card Chaos → UI → Add In-Game Pause Menu
/// </summary>
public static class InGamePauseUIBuilder
{
    const string GameScenePath = "Assets/Scenes/MainScene.unity";
    const string CanvasRootName = "InGamePauseCanvas";
    const string InGameArtFolder = "Assets/UI/ingame";
    const string MenuArtFolder = "Assets/UI/MainMenu/Art";

    [MenuItem("TCG Card Chaos/UI/Add In-Game Pause Menu")]
    public static void AddInGamePauseMenu()
    {
        if (!File.Exists(GameScenePath))
        {
            EditorUtility.DisplayDialog("In-Game Pause", "MainScene not found.", "OK");
            return;
        }

        MainMenuUIBuilder.EnsureLocalizationTable();
        EnsureInGameSprites();

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != GameScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        }

        Transform existing = FindPauseCanvas();
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        Canvas canvas = CreateCanvas();
        TMP_FontAsset font = FindPreferredFont();
        BuildPauseOverlay(canvas.transform, font);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, GameScenePath);
        Selection.activeGameObject = canvas.gameObject;
        EditorGUIUtility.PingObject(canvas.gameObject);
        Debug.Log("[InGamePauseUIBuilder] InGamePauseCanvas added to MainScene. Enable Panel_Pause in Hierarchy to edit.");
    }

    static Transform FindPauseCanvas()
    {
        GameObject found = GameObject.Find(CanvasRootName);
        return found != null ? found.transform : null;
    }

    static Canvas CreateCanvas()
    {
        var canvasGo = new GameObject(CanvasRootName);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
            | AdditionalCanvasShaderChannels.Normal
            | AdditionalCanvasShaderChannels.Tangent;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(
            RuntimeOverlayCanvasFactory.ReferenceWidth,
            RuntimeOverlayCanvasFactory.ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    static void BuildPauseOverlay(Transform canvas, TMP_FontAsset font)
    {
        var pauseView = canvas.gameObject.AddComponent<InGamePauseView>();
        RectTransform root = CreateUIObject("Panel_Pause", canvas);
        Stretch(root);

        RectTransform background = CreateUIObject("Background", root);
        Stretch(background);
        var backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.sprite = null;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.color = new Color(0.039f, 0.047f, 0.114f, 1f);
        backgroundImage.raycastTarget = true;

        BuildEscHint(root, font);
        BuildLogo(root);

        RectTransform panel = CreateUIObject("Panel", root);
        SetCenter(panel, new Vector2(0f, -120f), new Vector2(401f, 568f));
        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.sprite = LoadSprite(InGameArtFolder + "/panel_4_ingame.png");
        panelImage.type = Image.Type.Sliced;
        panelImage.color = Color.white;
        panelImage.raycastTarget = true;

        RectTransform column = CreateUIObject("ButtonColumn", panel);
        Stretch(column, new Vector4(36f, 36f, 36f, 36f));
        var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 14f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(0, 0, 8, 8);

        Button resume = BuildPauseButton(
            column,
            "Button_Resume",
            LoadSprite(MenuArtFolder + "/load_game_button.png"),
            new Color(0.45f, 0.84f, 0.15f),
            LocalizationKeys.PauseResume,
            font);
        Button save = BuildPauseButton(
            column,
            "Button_Save",
            LoadSprite(MenuArtFolder + "/settings_button.png"),
            new Color(0.18f, 0.70f, 0.95f),
            LocalizationKeys.PauseSave,
            font);
        Button load = BuildPauseButton(
            column,
            "Button_Load",
            LoadSprite(MenuArtFolder + "/settings_button.png"),
            new Color(0.18f, 0.70f, 0.95f),
            LocalizationKeys.MenuLoadGame,
            font);
        Button settings = BuildPauseButton(
            column,
            "Button_Settings",
            LoadSprite(MenuArtFolder + "/settings_button.png"),
            new Color(0.18f, 0.70f, 0.95f),
            LocalizationKeys.MenuSettings,
            font);
        Button quit = BuildPauseButton(
            column,
            "Button_Quit",
            LoadSprite(MenuArtFolder + "/quit_button.png"),
            new Color(0.91f, 0.32f, 0.20f),
            LocalizationKeys.MenuQuit,
            font);

        LoadGamePanelView loadPanel = null;
        try
        {
            loadPanel = MainMenuUIBuilder.CreateLoadGamePanel(canvas);
            if (loadPanel != null)
                loadPanel.transform.SetAsLastSibling();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[InGamePauseUIBuilder] Load Game overlay could not be added: " + exception.Message);
        }

        var serialized = new SerializedObject(pauseView);
        serialized.FindProperty("root").objectReferenceValue = root.gameObject;
        serialized.FindProperty("resumeButton").objectReferenceValue = resume;
        serialized.FindProperty("saveButton").objectReferenceValue = save;
        serialized.FindProperty("loadButton").objectReferenceValue = load;
        serialized.FindProperty("settingsButton").objectReferenceValue = settings;
        serialized.FindProperty("quitButton").objectReferenceValue = quit;
        serialized.FindProperty("loadGamePanel").objectReferenceValue = loadPanel;

        SaveGamePanelView savePanel = null;
        if (loadPanel != null)
        {
            savePanel = SaveGamePanelView.CreateFromLoadPanel(loadPanel, canvas);
            if (savePanel != null)
            {
                savePanel.transform.SetAsLastSibling();
                savePanel.gameObject.SetActive(false);
            }
        }

        serialized.FindProperty("saveGamePanel").objectReferenceValue = savePanel;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        root.gameObject.SetActive(false);
    }

    static void BuildEscHint(Transform parent, TMP_FontAsset font)
    {
        RectTransform row = CreateUIObject("EscHint", parent);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(0f, 1f);
        row.pivot = new Vector2(0f, 1f);
        row.anchoredPosition = new Vector2(40f, -36f);
        row.sizeDelta = new Vector2(220f, 64f);

        RectTransform icon = CreateUIObject("Icon", row);
        icon.anchorMin = new Vector2(0f, 0.5f);
        icon.anchorMax = new Vector2(0f, 0.5f);
        icon.pivot = new Vector2(0f, 0.5f);
        icon.anchoredPosition = Vector2.zero;
        icon.sizeDelta = new Vector2(55f, 61f);
        var iconImage = icon.gameObject.AddComponent<Image>();
        iconImage.sprite = LoadSprite(InGameArtFolder + "/esc_icon.png");
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        RectTransform labelRect = CreateUIObject("Label", row);
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(68f, 0f);
        labelRect.sizeDelta = new Vector2(140f, 48f);
        TMP_Text label = CreateText(labelRect, font, 36f, TextAlignmentOptions.Left, Color.white);
        label.raycastTarget = false;
        label.text = "Back";
        AddLocalizedText(labelRect.gameObject, LocalizationKeys.PauseBack);
    }

    static void BuildLogo(Transform parent)
    {
        RectTransform rect = CreateUIObject("Logo", parent);
        SetCenter(rect, new Vector2(0f, 294f), new Vector2(280f, 219f));
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = LoadSprite(MenuArtFolder + "/tcg_demo_logo.png");
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    static Button BuildPauseButton(
        Transform parent,
        string objectName,
        Sprite sprite,
        Color fallbackTint,
        string localizationKey,
        TMP_FontAsset font)
    {
        RectTransform rect = CreateUIObject(objectName, parent);
        var layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 300f;
        layout.preferredHeight = 90f;
        layout.minWidth = 300f;
        layout.minHeight = 90f;
        rect.sizeDelta = new Vector2(300f, 90f);

        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = sprite != null ? Color.white : fallbackTint;
        image.preserveAspect = true;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        RectTransform labelRect = CreateUIObject("Label", rect);
        Stretch(labelRect, new Vector4(16f, 8f, 16f, 10f));
        TMP_Text label = CreateText(labelRect, font, 36f, TextAlignmentOptions.Center, Color.white);
        label.raycastTarget = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 36f;
        AddLocalizedText(labelRect.gameObject, localizationKey);
        return button;
    }

    static void EnsureInGameSprites()
    {
        if (!AssetDatabase.IsValidFolder("Assets/UI"))
            AssetDatabase.CreateFolder("Assets", "UI");
        if (!AssetDatabase.IsValidFolder(InGameArtFolder))
            AssetDatabase.CreateFolder("Assets/UI", "ingame");

        ConfigureSprite(InGameArtFolder + "/panel_4_ingame.png", 56f);
        ConfigureSprite(InGameArtFolder + "/esc_icon.png", 0f);
        ConfigureSprite(MenuArtFolder + "/load_game_button.png", 40f);
        ConfigureSprite(MenuArtFolder + "/settings_button.png", 40f);
        ConfigureSprite(MenuArtFolder + "/quit_button.png", 40f);
        ConfigureSprite(MenuArtFolder + "/tcg_demo_logo.png", 0f);
    }

    static void ConfigureSprite(string assetPath, float sliceBorder)
    {
        if (!File.Exists(assetPath))
            return;

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.spriteBorder = sliceBorder > 0f
            ? new Vector4(sliceBorder, sliceBorder, sliceBorder, sliceBorder)
            : Vector4.zero;
        importer.SaveAndReimport();
    }

    static Sprite LoadSprite(string assetPath)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    static RectTransform CreateUIObject(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rect, Vector4 padding = default)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding.x, padding.w);
        rect.offsetMax = new Vector2(-padding.z, -padding.y);
    }

    static void SetCenter(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    static TMP_Text CreateText(
        RectTransform rect,
        TMP_FontAsset font,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.richText = true;
        TMP_FontAsset menuFont = font;
        if (menuFont == null || menuFont.name.IndexOf("Baloo", System.StringComparison.OrdinalIgnoreCase) < 0)
            menuFont = FindPreferredFont();
        if (menuFont != null)
            text.font = menuFont;
        return text;
    }

    static void AddLocalizedText(GameObject target, string key)
    {
        var localized = target.AddComponent<LocalizedText>();
        var serialized = new SerializedObject(localized);
        serialized.FindProperty("key").stringValue = key;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static TMP_FontAsset FindPreferredFont()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        TMP_FontAsset fallback = null;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null || font.name.IndexOf("Baloo", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (font.name.IndexOf("ExtraBold", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return font;

            fallback ??= font;
        }

        return fallback;
    }
}

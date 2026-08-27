using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the always-visible in-game HUD in MainScene.
/// Menu: TCG Card Chaos → UI → Add In-Game HUD
/// </summary>
public static class InGameHudUIBuilder
{
    const string GameScenePath = "Assets/Scenes/MainScene.unity";
    const string HudArtFolder = "Assets/UI/ingame/Hud";
    const string CanvasRootName = "InGameHudCanvas";

    [MenuItem("TCG Card Chaos/UI/Add In-Game HUD")]
    public static void AddInGameHud()
    {
        if (!File.Exists(GameScenePath))
        {
            EditorUtility.DisplayDialog("In-Game HUD", "MainScene not found.", "OK");
            return;
        }

        EnsureFolders();
        ConfigureSprites();

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != GameScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        }

        Transform existing = FindHudCanvas();
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        Canvas canvas = CreateCanvas();
        TMP_FontAsset font = FindPreferredFont();
        BuildHud(canvas.transform, font);
        BindView(canvas);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, GameScenePath);
        Selection.activeGameObject = canvas.gameObject;
        EditorGUIUtility.PingObject(canvas.gameObject);
        Debug.Log("[InGameHudUIBuilder] InGameHudCanvas added to MainScene. Drop art in " + HudArtFolder);
    }

    static Transform FindHudCanvas()
    {
        GameObject found = GameObject.Find(CanvasRootName);
        return found != null ? found.transform : null;
    }

    static Canvas CreateCanvas()
    {
        var canvasGo = new GameObject(CanvasRootName);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
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

    static void BuildHud(Transform canvas, TMP_FontAsset font)
    {
        Sprite statsSprite = LoadSprite(HudArtFolder + "/hud_stats_panel.png");
        Sprite handSprite = LoadSprite(HudArtFolder + "/hud_hand_panel.png");

        RectTransform topLeft = CreateUIObject("Panel_TopLeft", canvas);
        topLeft.anchorMin = new Vector2(0f, 1f);
        topLeft.anchorMax = new Vector2(0f, 1f);
        topLeft.pivot = new Vector2(0f, 1f);
        topLeft.anchoredPosition = new Vector2(28f, -28f);
        topLeft.sizeDelta = new Vector2(257f, 161f);

        RectTransform topBackground = CreateUIObject("Background", topLeft);
        Stretch(topBackground);
        var topBackgroundImage = topBackground.gameObject.AddComponent<Image>();
        topBackgroundImage.sprite = statsSprite;
        topBackgroundImage.preserveAspect = false;
        topBackgroundImage.color = statsSprite != null ? Color.white : new Color(0f, 0f, 0f, 0.55f);
        topBackgroundImage.raycastTarget = false;

        TMP_Text shelvesValue = CreateValueText(
            topLeft,
            "ShelvesValue",
            new Vector2(158f, -52f),
            new Vector2(90f, 30f),
            font);
        TMP_Text cardsValue = CreateValueText(
            topLeft,
            "CardsValue",
            new Vector2(158f, -112f),
            new Vector2(90f, 30f),
            font);

        RectTransform handPanel = CreateUIObject("Panel_Hand", canvas);
        handPanel.anchorMin = new Vector2(1f, 0f);
        handPanel.anchorMax = new Vector2(1f, 0f);
        handPanel.pivot = new Vector2(1f, 0f);
        handPanel.anchoredPosition = new Vector2(-28f, 28f);
        handPanel.sizeDelta = new Vector2(127f, 139f);

        RectTransform handBackground = CreateUIObject("Background", handPanel);
        Stretch(handBackground);
        var handBackgroundImage = handBackground.gameObject.AddComponent<Image>();
        handBackgroundImage.sprite = handSprite;
        handBackgroundImage.preserveAspect = false;
        handBackgroundImage.color = handSprite != null ? Color.white : new Color(0f, 0f, 0f, 0.55f);
        handBackgroundImage.raycastTarget = false;

        TMP_Text handValue = CreateValueText(
            handPanel,
            "HandValue",
            new Vector2(0f, 16f),
            new Vector2(96f, 30f),
            font,
            TextAlignmentOptions.Center);

        shelvesValue.text = "0 / " + GameHudLimits.MaxShelves;
        cardsValue.text = "0 / " + GameHudLimits.MaxPlacedCards;
        handValue.text = "0 / " + CardDimensions.MaxHandSize;
    }

    static void BindView(Canvas canvas)
    {
        var view = canvas.gameObject.AddComponent<InGameHudView>();
        TMP_Text shelves = canvas.transform.Find("Panel_TopLeft/ShelvesValue")?.GetComponent<TMP_Text>();
        TMP_Text cards = canvas.transform.Find("Panel_TopLeft/CardsValue")?.GetComponent<TMP_Text>();
        TMP_Text hand = canvas.transform.Find("Panel_Hand/HandValue")?.GetComponent<TMP_Text>();

        var serialized = new SerializedObject(view);
        serialized.FindProperty("shelvesValueText").objectReferenceValue = shelves;
        serialized.FindProperty("cardsValueText").objectReferenceValue = cards;
        serialized.FindProperty("handValueText").objectReferenceValue = hand;
        serialized.FindProperty("maxShelves").intValue = GameHudLimits.MaxShelves;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static TMP_Text CreateValueText(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size,
        TMP_FontAsset font,
        TextAlignmentOptions alignment = TextAlignmentOptions.Right)
    {
        RectTransform rect = CreateUIObject(objectName, parent);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 24f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = 24f;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        if (font != null)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
        }

        return text;
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/UI"))
            AssetDatabase.CreateFolder("Assets", "UI");
        if (!AssetDatabase.IsValidFolder("Assets/UI/ingame"))
            AssetDatabase.CreateFolder("Assets/UI", "ingame");
        if (!AssetDatabase.IsValidFolder(HudArtFolder))
            AssetDatabase.CreateFolder("Assets/UI/ingame", "Hud");
    }

    static void ConfigureSprites()
    {
        ConfigureSprite(HudArtFolder + "/hud_stats_panel.png");
        ConfigureSprite(HudArtFolder + "/hud_hand_panel.png");
    }

    static void ConfigureSprite(string assetPath)
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
        importer.spriteBorder = Vector4.zero;
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

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
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

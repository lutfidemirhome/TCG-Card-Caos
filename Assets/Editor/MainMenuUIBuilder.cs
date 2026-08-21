using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds the authored main menu hierarchy in MenuScene from the approved mockup layout.
/// Every rect is positioned in 1920x1080 reference space so art slices can be dropped straight
/// onto the matching Image component without moving anything.
/// Menu: TCG Card Caos → UI → Build Main Menu UI
/// </summary>
public static class MainMenuUIBuilder
{
    const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
    const string LocalizationFolder = "Assets/Resources/Localization";
    const string UiArtFolder = "Assets/UI/MainMenu/Art";
    const string MenuPrefabPath = "Assets/UI/MainMenu/MainMenuCanvas.prefab";
    const string CanvasRootName = "MainMenuCanvas";
    const string LegacyMenuRootName = "MainMenu";

    [MenuItem("TCG Card Caos/UI/Open Main Menu Scene")]
    public static void OpenMainMenuScene()
    {
        if (!File.Exists(MenuScenePath))
        {
            EditorUtility.DisplayDialog(
                "Main Menu",
                "MenuScene not found.\n\nRun: TCG Card Caos → UI → Build Main Menu UI",
                "OK");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path == MenuScenePath)
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
    }

    [MenuItem("TCG Card Caos/UI/Add Continue Button To Menu")]
    public static void AddContinueButtonToMenu()
    {
        if (!File.Exists(MenuScenePath))
        {
            EditorUtility.DisplayDialog(
                "Main Menu",
                "MenuScene not found.\n\nRun: TCG Card Caos → UI → Build Main Menu UI",
                "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureLocalizationTable();

        UnityEngine.SceneManagement.Scene scene =
            EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);

        Transform canvas = FindMenuCanvas();
        if (canvas == null)
        {
            Debug.LogError("[MainMenuUIBuilder] MainMenuCanvas not found in MenuScene.");
            return;
        }

        Transform loadGame = canvas.Find("Button_LoadGame");
        if (loadGame == null)
        {
            Debug.LogError("[MainMenuUIBuilder] Button_LoadGame not found. Add Load Game first.");
            return;
        }

        Transform existingContinue = canvas.Find("Button_Continue");
        if (existingContinue != null)
            Object.DestroyImmediate(existingContinue.gameObject);

        GameObject continueGo = Object.Instantiate(loadGame.gameObject, canvas);
        continueGo.name = "Button_Continue";

        RectTransform continueRect = continueGo.GetComponent<RectTransform>();
        RectTransform newGameRect = canvas.Find("Button_NewGame")?.GetComponent<RectTransform>();
        float y = newGameRect != null ? newGameRect.anchoredPosition.y + 105f : -17f;
        continueRect.anchoredPosition = new Vector2(0f, y);

        Transform label = continueGo.transform.Find("Label");
        if (label != null)
        {
            var localized = label.GetComponent<LocalizedText>();
            if (localized != null)
            {
                var localizedObject = new SerializedObject(localized);
                localizedObject.FindProperty("key").stringValue = LocalizationKeys.MenuContinue;
                localizedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        MainMenuView view = canvas.GetComponent<MainMenuView>();
        if (view != null)
        {
            var viewObject = new SerializedObject(view);
            viewObject.FindProperty("continueButton").objectReferenceValue = continueGo.GetComponent<Button>();
            Transform logo = canvas.Find("Logo");
            if (logo != null)
                viewObject.FindProperty("logoRect").objectReferenceValue = logo.GetComponent<RectTransform>();
            viewObject.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, MenuScenePath);

        if (File.Exists(MenuPrefabPath))
            PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, MenuPrefabPath);

        Selection.activeGameObject = continueGo;
        Debug.Log("[MainMenuUIBuilder] Button_Continue added above New Game (copied from Load Game).");
    }

    [MenuItem("TCG Card Caos/UI/Build Main Menu UI")]
    public static void BuildMainMenuUI()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        LocalizationTable table = EnsureLocalizationTable();

        UnityEngine.SceneManagement.Scene scene = File.Exists(MenuScenePath)
            ? EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RemoveExistingRoots();
        EnsureMenuCamera();
        EnsureEventSystem();

        TMP_FontAsset font = FindPreferredFont();
        Canvas canvas = CreateCanvas();
        var view = canvas.gameObject.AddComponent<MainMenuView>();

        BuildBackground(canvas.transform);
        BuildLogo(canvas.transform);

        Button continueBtn = BuildMenuButton(canvas.transform, "Button_Continue", LocalizationKeys.MenuContinue,
            new Vector2(0f, -17f), new Color(0.45f, 0.84f, 0.15f), font);
        Button newGame = BuildMenuButton(canvas.transform, "Button_NewGame", LocalizationKeys.MenuNewGame,
            new Vector2(0f, -122f), new Color(0.96f, 0.58f, 0.11f), font);
        Button loadGame = BuildMenuButton(canvas.transform, "Button_LoadGame", LocalizationKeys.MenuLoadGame,
            new Vector2(0f, -227f), new Color(0.45f, 0.84f, 0.15f), font);
        Button settings = BuildMenuButton(canvas.transform, "Button_Settings", LocalizationKeys.MenuSettings,
            new Vector2(0f, -332f), new Color(0.18f, 0.70f, 0.95f), font);
        Button quit = BuildMenuButton(canvas.transform, "Button_Quit", LocalizationKeys.MenuQuit,
            new Vector2(0f, -437f), new Color(0.91f, 0.32f, 0.20f), font);

        TMP_Text versionText = BuildVersionLabel(canvas.transform, font);
        Button feedback = BuildFeedbackButton(canvas.transform, font);

        RectTransform panel = BuildRoadmapPanel(canvas.transform, font,
            out Button discord, out Button tiktok, out Button instagram, out Button youtube);

        AssignViewReferences(view, canvas.transform, continueBtn, newGame, loadGame, settings, quit, feedback, versionText,
            discord, tiktok, instagram, youtube);

        EnsureFolder("Assets/UI");
        EnsureFolder("Assets/UI/MainMenu");
        EnsureFolder(UiArtFolder);
        PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, MenuPrefabPath);

        Selection.activeGameObject = canvas.gameObject;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, MenuScenePath);
        EnsureBuildSettings();

        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "Main menu built.\n"
            + "  Scene: " + MenuScenePath + "  (Hierarchy → MainMenuCanvas)\n"
            + "  Prefab: " + MenuPrefabPath + "\n"
            + "  Art drop folder: " + UiArtFolder + "\n"
            + (font != null
                ? "  Font: " + font.name
                : "  Font: assign Baloo2-ExtraBold SDF to TMP texts if missing."));
    }

    static void RemoveExistingRoots()
    {
        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
                continue;

            if (root.name == CanvasRootName || root.name == LegacyMenuRootName)
                Object.DestroyImmediate(root);
        }
    }

    static void EnsureMenuCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            camera = cameraGo.AddComponent<Camera>();
        }

        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.08f, 0.10f, 1f);
        camera.depth = -1;
        camera.orthographic = true;

        if (camera.GetComponent<AudioListener>() == null)
            camera.gameObject.AddComponent<AudioListener>();
    }

    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
            return;

        var eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<StandaloneInputModule>();
    }

    static Canvas CreateCanvas()
    {
        var canvasGo = new GameObject(CanvasRootName);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

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

    static void BuildBackground(Transform parent)
    {
        RectTransform rect = CreateUIObject("Background", parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.17f, 0.19f, 1f);
        image.raycastTarget = false;
    }

    static void BuildLogo(Transform parent)
    {
        RectTransform rect = CreateUIObject("Logo", parent);
        SetCenterAnchored(rect, new Vector2(0f, 340f), new Vector2(560f, 400f));

        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.15f);
        image.raycastTarget = false;
        image.preserveAspect = true;
    }

    static Button BuildMenuButton(
        Transform parent,
        string objectName,
        string localizationKey,
        Vector2 anchoredPosition,
        Color placeholderColor,
        TMP_FontAsset font)
    {
        RectTransform rect = CreateUIObject(objectName, parent);
        SetCenterAnchored(rect, anchoredPosition, new Vector2(300f, 90f));

        var image = rect.gameObject.AddComponent<Image>();
        image.color = placeholderColor;
        image.type = Image.Type.Sliced;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        RectTransform labelRect = CreateUIObject("Label", rect);
        StretchToParent(labelRect, new Vector4(18f, 8f, 18f, 12f));

        TMP_Text label = CreateText(labelRect, font, 40f, TextAlignmentOptions.Center, Color.white);
        EnableAutoSize(label, 22f, 40f);
        AddLocalizedText(labelRect.gameObject, localizationKey);

        return button;
    }

    static TMP_Text BuildVersionLabel(Transform parent, TMP_FontAsset font)
    {
        RectTransform rect = CreateUIObject("Text_Version", parent);
        SetCornerAnchored(rect, new Vector2(0f, 0f), new Vector2(253f, 161f), new Vector2(240f, 44f));

        TMP_Text text = CreateText(rect, font, 26f, TextAlignmentOptions.Center, Color.white);
        text.text = "v0.30";
        text.raycastTarget = false;
        return text;
    }

    static Button BuildFeedbackButton(Transform parent, TMP_FontAsset font)
    {
        RectTransform rect = CreateUIObject("Button_Feedback", parent);
        SetCornerAnchored(rect, new Vector2(0f, 0f), new Vector2(253f, 103f), new Vector2(216f, 68f));

        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.85f, 0.86f, 0.88f, 1f);
        image.type = Image.Type.Sliced;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        RectTransform labelRect = CreateUIObject("Label", rect);
        StretchToParent(labelRect, new Vector4(10f, 6f, 10f, 8f));

        TMP_Text label = CreateText(labelRect, font, 26f, TextAlignmentOptions.Center, new Color(0.13f, 0.14f, 0.16f));
        EnableAutoSize(label, 16f, 26f);
        AddLocalizedText(labelRect.gameObject, LocalizationKeys.MenuFeedback);

        return button;
    }

    static RectTransform BuildRoadmapPanel(
        Transform parent,
        TMP_FontAsset font,
        out Button discord,
        out Button tiktok,
        out Button instagram,
        out Button youtube)
    {
        RectTransform panel = CreateUIObject("Panel_Roadmap", parent);
        SetCornerAnchored(panel, new Vector2(1f, 1f), new Vector2(-251f, -679f), new Vector2(326f, 690f));

        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(1f, 1f, 1f, 0.92f);
        panelImage.type = Image.Type.Sliced;

        // Header ribbon
        RectTransform headerRect = CreateUIObject("Header", panel);
        SetTopAnchored(headerRect, -52f, new Vector2(310f, 88f));

        var headerImage = headerRect.gameObject.AddComponent<Image>();
        headerImage.color = new Color(1f, 0.80f, 0.20f, 1f);
        headerImage.type = Image.Type.Sliced;

        RectTransform headerLabelRect = CreateUIObject("Label", headerRect);
        StretchToParent(headerLabelRect, new Vector4(10f, 6f, 10f, 6f));

        TMP_Text headerLabel = CreateText(headerLabelRect, font, 24f, TextAlignmentOptions.Center,
            new Color(0.12f, 0.12f, 0.14f));
        EnableAutoSize(headerLabel, 14f, 24f);
        AddLocalizedText(headerLabelRect.gameObject, LocalizationKeys.MenuRoadmapTitle, uppercase: true);

        // Feature bullet list — a single text block so translators get one coherent string.
        RectTransform itemsRect = CreateUIObject("Text_Items", panel);
        SetTopAnchored(itemsRect, -332f, new Vector2(300f, 431f));

        TMP_Text items = CreateText(itemsRect, font, 20f, TextAlignmentOptions.TopLeft,
            new Color(0.12f, 0.12f, 0.14f));
        items.raycastTarget = false;
        items.lineSpacing = 12f;
        EnableAutoSize(items, 12f, 20f);
        AddLocalizedText(itemsRect.gameObject, LocalizationKeys.MenuRoadmapItems);

        // Follow us
        RectTransform followRect = CreateUIObject("Text_FollowUs", panel);
        SetTopAnchored(followRect, -588f, new Vector2(300f, 44f));

        TMP_Text follow = CreateText(followRect, font, 20f, TextAlignmentOptions.Center,
            new Color(0.42f, 0.44f, 0.47f));
        follow.raycastTarget = false;
        EnableAutoSize(follow, 12f, 20f);
        AddLocalizedText(followRect.gameObject, LocalizationKeys.MenuFollowUs, uppercase: true);

        // Social icon row
        RectTransform socialRow = CreateUIObject("SocialRow", panel);
        SetTopAnchored(socialRow, -643f, new Vector2(300f, 64f));

        discord = BuildSocialButton(socialRow, "Button_Discord", -114f, new Color(0.35f, 0.40f, 0.95f));
        tiktok = BuildSocialButton(socialRow, "Button_TikTok", -38f, new Color(0.10f, 0.10f, 0.12f));
        instagram = BuildSocialButton(socialRow, "Button_Instagram", 38f, new Color(0.86f, 0.30f, 0.60f));
        youtube = BuildSocialButton(socialRow, "Button_YouTube", 114f, new Color(0.90f, 0.20f, 0.18f));

        return panel;
    }

    static Button BuildSocialButton(Transform parent, string objectName, float offsetX, Color placeholderColor)
    {
        RectTransform rect = CreateUIObject(objectName, parent);
        SetCenterAnchored(rect, new Vector2(offsetX, 0f), new Vector2(56f, 56f));

        var image = rect.gameObject.AddComponent<Image>();
        image.color = placeholderColor;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    static void AssignViewReferences(
        MainMenuView view,
        Transform canvasRoot,
        Button continueButton,
        Button newGame,
        Button loadGame,
        Button settings,
        Button quit,
        Button feedback,
        TMP_Text versionText,
        Button discord,
        Button tiktok,
        Button instagram,
        Button youtube)
    {
        Transform logo = canvasRoot != null ? canvasRoot.Find("Logo") : null;
        RectTransform logoRect = logo != null ? logo.GetComponent<RectTransform>() : null;

        var serialized = new SerializedObject(view);
        serialized.FindProperty("continueButton").objectReferenceValue = continueButton;
        serialized.FindProperty("newGameButton").objectReferenceValue = newGame;
        serialized.FindProperty("loadGameButton").objectReferenceValue = loadGame;
        serialized.FindProperty("settingsButton").objectReferenceValue = settings;
        serialized.FindProperty("quitButton").objectReferenceValue = quit;
        serialized.FindProperty("feedbackButton").objectReferenceValue = feedback;
        serialized.FindProperty("logoRect").objectReferenceValue = logoRect;
        serialized.FindProperty("versionText").objectReferenceValue = versionText;
        serialized.FindProperty("discordButton").objectReferenceValue = discord;
        serialized.FindProperty("tiktokButton").objectReferenceValue = tiktok;
        serialized.FindProperty("instagramButton").objectReferenceValue = instagram;
        serialized.FindProperty("youtubeButton").objectReferenceValue = youtube;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static Transform FindMenuCanvas()
    {
        GameObject canvasGo = GameObject.Find(CanvasRootName);
        return canvasGo != null ? canvasGo.transform : null;
    }

    // ---------- layout helpers ----------

    static RectTransform CreateUIObject(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void SetCenterAnchored(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    /// <summary>Anchors to a canvas corner so the element keeps its screen margin on any aspect.</summary>
    static void SetCornerAnchored(RectTransform rect, Vector2 corner, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = corner;
        rect.anchorMax = corner;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    static void SetTopAnchored(RectTransform rect, float anchoredY, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(0f, anchoredY);
    }

    static void StretchToParent(RectTransform rect, Vector4 padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding.x, padding.w);
        rect.offsetMax = new Vector2(-padding.z, -padding.y);
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

        if (font != null)
            text.font = font;

        return text;
    }

    /// <summary>
    /// Auto sizing keeps long translations (German, Russian) inside the authored art instead of
    /// overflowing the button slice.
    /// </summary>
    static void EnableAutoSize(TMP_Text text, float min, float max)
    {
        text.enableAutoSizing = true;
        text.fontSizeMin = min;
        text.fontSizeMax = max;
    }

    static void AddLocalizedText(GameObject target, string key, bool uppercase = false)
    {
        var localized = target.AddComponent<LocalizedText>();
        var serialized = new SerializedObject(localized);
        serialized.FindProperty("key").stringValue = key;
        serialized.FindProperty("uppercase").boolValue = uppercase;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Picks the Baloo 2 TMP font asset when it exists, preferring an ExtraBold weight.
    /// Returns null before the font has been generated, leaving TMP's default in place.
    /// </summary>
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

    // ---------- localization table ----------

    [MenuItem("TCG Card Caos/UI/Create Or Update Localization Table")]
    public static LocalizationTable EnsureLocalizationTable()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(LocalizationFolder);

        string assetPath = LocalizationFolder + "/" + LocalizationTable.AssetName + ".asset";
        var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(assetPath);

        if (table == null)
        {
            table = ScriptableObject.CreateInstance<LocalizationTable>();
            AssetDatabase.CreateAsset(table, assetPath);
        }

        table.EditorEnsureRowSizes();

        table.EditorEnsureKey(LocalizationKeys.MenuContinue, "Continue", "Devam Et");
        table.EditorEnsureKey(LocalizationKeys.MenuNewGame, "New Game", "Yeni Oyun");
        table.EditorEnsureKey(LocalizationKeys.MenuLoadGame, "Load Game", "Oyun Yükle");
        table.EditorEnsureKey(LocalizationKeys.MenuSettings, "Settings", "Ayarlar");
        table.EditorEnsureKey(LocalizationKeys.MenuQuit, "Quit", "Çıkış");
        table.EditorEnsureKey(LocalizationKeys.MenuFeedback, "Feedback", "Geri Bildirim");
        table.EditorEnsureKey(LocalizationKeys.MenuFollowUs, "Follow Us", "Bizi Takip Edin");
        table.EditorEnsureKey(LocalizationKeys.MenuRoadmapTitle,
            "Planned For Full Release", "Tam Sürümde Planlanan");
        table.EditorEnsureKey(LocalizationKeys.MenuRoadmapItems, RoadmapItemsEnglish, RoadmapItemsTurkish);

        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        return table;
    }

    const string RoadmapItemsEnglish =
        "• 5,000+ Cards\n"
        + "• Larger Play Area\n"
        + "• More Card Categories\n"
        + "• More Shelves & Storage\n"
        + "• Additional Card Packs\n"
        + "• More Graded Cards\n"
        + "• New Rare Card Types\n"
        + "• Skills & Upgrades\n"
        + "• Achievements\n"
        + "• Quality-of-Life Improvements\n"
        + "…and more!";

    const string RoadmapItemsTurkish =
        "• 5.000+ Kart\n"
        + "• Daha Geniş Oyun Alanı\n"
        + "• Daha Fazla Kart Kategorisi\n"
        + "• Daha Fazla Raf ve Depolama\n"
        + "• Ek Kart Paketleri\n"
        + "• Daha Fazla Dereceli Kart\n"
        + "• Yeni Nadir Kart Türleri\n"
        + "• Yetenekler ve Geliştirmeler\n"
        + "• Başarımlar\n"
        + "• Kullanım Kolaylığı İyileştirmeleri\n"
        + "…ve daha fazlası!";

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int lastSlash = path.LastIndexOf('/');
        string parent = path.Substring(0, lastSlash);
        string folderName = path.Substring(lastSlash + 1);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    static void EnsureBuildSettings()
    {
        const string gameScenePath = "Assets/Scenes/MainScene.unity";
        if (!File.Exists(gameScenePath))
            return;

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(gameScenePath, true),
        };
    }
}

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
/// Menu: TCG Card Chaos → UI → Build Main Menu UI
/// </summary>
public static class MainMenuUIBuilder
{
    const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
    const string LocalizationFolder = "Assets/Resources/Localization";
    const string UiArtFolder = "Assets/UI/MainMenu/Art";
    const string LoadGameArtFolder = "Assets/UI/LoadGame/Art";
    const string MenuPrefabPath = "Assets/UI/MainMenu/MainMenuCanvas.prefab";
    const string CanvasRootName = "MainMenuCanvas";
    const string LegacyMenuRootName = "MainMenu";

    [MenuItem("TCG Card Chaos/UI/Open Main Menu Scene")]
    public static void OpenMainMenuScene()
    {
        if (!File.Exists(MenuScenePath))
        {
            EditorUtility.DisplayDialog(
                "Main Menu",
                "MenuScene not found.\n\nRun: TCG Card Chaos → UI → Build Main Menu UI",
                "OK");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path == MenuScenePath)
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
    }

    [MenuItem("TCG Card Chaos/UI/Add Continue Button To Menu")]
    public static void AddContinueButtonToMenu()
    {
        if (!File.Exists(MenuScenePath))
        {
            EditorUtility.DisplayDialog(
                "Main Menu",
                "MenuScene not found.\n\nRun: TCG Card Chaos → UI → Build Main Menu UI",
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

    [MenuItem("TCG Card Chaos/UI/Add Load Game Panel")]
    public static void AddLoadGamePanelToMenu()
    {
        if (!File.Exists(MenuScenePath))
        {
            EditorUtility.DisplayDialog(
                "Load Game",
                "MenuScene not found.\n\nRun: TCG Card Chaos → UI → Build Main Menu UI",
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

        Transform existing = canvas.Find("Panel_LoadGame");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        LoadGamePanelView panel = BuildLoadGamePanel(canvas, FindPreferredFont());
        MainMenuView view = canvas.GetComponent<MainMenuView>();
        AssignLoadGamePanel(view, panel);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, MenuScenePath);

        if (File.Exists(MenuPrefabPath))
            PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, MenuPrefabPath);

        Selection.activeGameObject = panel.gameObject;
        Debug.Log("[MainMenuUIBuilder] Panel_LoadGame added. Dress Images in Hierarchy, then wire save data later.");
    }

    [MenuItem("TCG Card Chaos/UI/Add Load Game Confirm Dialog")]
    public static void AddLoadGameConfirmDialog()
    {
        if (!File.Exists(MenuScenePath))
        {
            EditorUtility.DisplayDialog(
                "Load Confirm",
                "MenuScene not found.\n\nRun: TCG Card Chaos → UI → Build Main Menu UI",
                "OK");
            return;
        }

        EnsureLocalizationTable();
        EnsureLoadGameConfirmPlaceholders();

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != MenuScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        }

        Transform canvas = FindMenuCanvas();
        if (canvas == null)
        {
            Debug.LogError("[MainMenuUIBuilder] MainMenuCanvas not found in MenuScene.");
            return;
        }

        Transform loadPanel = canvas.Find("Panel_LoadGame");
        if (loadPanel == null)
        {
            EditorUtility.DisplayDialog(
                "Load Confirm",
                "Panel_LoadGame not found.\n\nRun: TCG Card Chaos → UI → Add Load Game Panel",
                "OK");
            return;
        }

        Transform existing = loadPanel.Find("Panel_LoadConfirm");
        LoadGameConfirmView confirm = existing != null
            ? CompleteLoadGameConfirm(existing, FindPreferredFont())
            : BuildLoadGameConfirm(loadPanel, FindPreferredFont());

        LoadGamePanelView panelView = loadPanel.GetComponent<LoadGamePanelView>();
        AssignLoadGameConfirm(panelView, confirm);
        EnsureLoadGameBackground(loadPanel);
        EnsureLoadGameCancelLabel(loadPanel);
        EnsureLoadGameDrawOrder(loadPanel);

        // Make sure every authored slot row is wired into the panel array.
        LoadGameSlotView[] allSlots = loadPanel.GetComponentsInChildren<LoadGameSlotView>(true);
        if (panelView != null && allSlots != null && allSlots.Length > 0)
        {
            var panelSerialized = new SerializedObject(panelView);
            SerializedProperty slotsProp = panelSerialized.FindProperty("slots");
            slotsProp.arraySize = allSlots.Length;
            for (int i = 0; i < allSlots.Length; i++)
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = allSlots[i];
            panelSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, MenuScenePath);

        if (File.Exists(MenuPrefabPath))
            PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, MenuPrefabPath);

        Selection.activeGameObject = confirm.gameObject;
        EditorGUIUtility.PingObject(confirm.gameObject);
        Debug.Log(
            "[MainMenuUIBuilder] Panel_LoadConfirm added under Panel_LoadGame. "
            + "Replace placeholder Images: confirm_band / yes_button / no_button in Assets/UI/LoadGame/Art.");
    }

    [MenuItem("TCG Card Chaos/UI/Select Load Game Confirm For Editing")]
    public static void SelectLoadGameConfirmForEditing()
    {
        EnsureLocalizationTable();
        EnsureLoadGameConfirmPlaceholders();

        Transform loadPanel = FindLoadGamePanelForEditing();
        if (loadPanel == null)
        {
            EditorUtility.DisplayDialog(
                "Load Confirm",
                "Panel_LoadGame not found.\n\nRun: TCG Card Chaos → UI → Add Load Game Panel",
                "OK");
            return;
        }

        Transform confirm = loadPanel.Find("Panel_LoadConfirm");
        if (confirm == null || !IsLoadGameConfirmComplete(confirm))
        {
            if (confirm != null)
                Object.DestroyImmediate(confirm.gameObject);

            LoadGameConfirmView built = BuildLoadGameConfirm(loadPanel, FindPreferredFont());
            AssignLoadGameConfirm(loadPanel.GetComponent<LoadGamePanelView>(), built);
            confirm = built.transform;
            EditorSceneManager.MarkSceneDirty(loadPanel.gameObject.scene);
        }
        else
        {
            LoadGameConfirmView view = confirm.GetComponent<LoadGameConfirmView>();
            if (view == null)
                view = confirm.gameObject.AddComponent<LoadGameConfirmView>();
            WireLoadGameConfirmReferences(view, confirm);
            AssignLoadGameConfirm(loadPanel.GetComponent<LoadGamePanelView>(), view);
        }

        EnsureLoadGameBackground(loadPanel);
        EnsureLoadGameCancelLabel(loadPanel);
        EnsureLoadGameDrawOrder(loadPanel);

        Selection.activeGameObject = confirm.gameObject;
        EditorGUIUtility.PingObject(confirm.gameObject);

        Debug.Log(
            "[MainMenuUIBuilder] Panel_LoadConfirm is under Panel_LoadGame (inactive by default).\n"
            + "Enable Panel_LoadConfirm in Hierarchy when you want to preview or edit it.\n"
            + "Labels: Band → ButtonRow → Button_Yes/No → Label");
    }

    static Transform FindLoadGamePanelForEditing()
    {
        Transform loadPanel = FindLoadGamePanelInLoadedScenes();
        if (loadPanel != null)
            return loadPanel;

        if (!File.Exists(MenuScenePath))
            return null;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return null;

        EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        return FindLoadGamePanelInLoadedScenes();
    }

    static Transform FindLoadGamePanelInLoadedScenes()
    {
        Transform canvas = FindMenuCanvas();
        if (canvas != null)
        {
            Transform loadPanel = canvas.Find("Panel_LoadGame");
            if (loadPanel != null)
                return loadPanel;
        }

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != CanvasRootName && root.name != LegacyMenuRootName)
                    continue;

                Transform loadPanel = root.transform.Find("Panel_LoadGame");
                if (loadPanel != null)
                    return loadPanel;
            }
        }

        return null;
    }

    [MenuItem("TCG Card Chaos/UI/Fix Load Game Scroll")]
    public static void FixLoadGameScrollInMenu()
    {
        if (!File.Exists(MenuScenePath))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        UnityEngine.SceneManagement.Scene scene =
            EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);

        Transform panel = FindMenuCanvas()?.Find("Panel_LoadGame");
        if (panel == null)
        {
            Debug.LogError("[MainMenuUIBuilder] Panel_LoadGame not found.");
            return;
        }

        Transform listFrame = panel.Find("ListFrame");
        if (listFrame == null)
        {
            Debug.LogError("[MainMenuUIBuilder] ListFrame not found.");
            return;
        }

        Transform scrollbar = listFrame.Find("Scrollbar");
        if (scrollbar != null)
        {
            Transform handle = scrollbar.Find("Sliding Area/Handle");
            if (handle != null)
            {
                RectTransform handleRect = (RectTransform)handle;
                handleRect.localScale = Vector3.one;

                Image handleImage = handle.GetComponent<Image>();
                if (handleImage != null)
                    handleImage.preserveAspect = true;
            }

            LoadGameCircularScrollThumb thumb = scrollbar.GetComponent<LoadGameCircularScrollThumb>();
            if (thumb == null)
                thumb = scrollbar.gameObject.AddComponent<LoadGameCircularScrollThumb>();

            Scrollbar scrollbarComponent = scrollbar.GetComponent<Scrollbar>();
            Transform slidingArea = scrollbar.Find("Sliding Area");
            if (scrollbarComponent != null && slidingArea != null && handle != null)
            {
                var serialized = new SerializedObject(thumb);
                serialized.FindProperty("scrollbar").objectReferenceValue = scrollbarComponent;
                serialized.FindProperty("slidingArea").objectReferenceValue = slidingArea;
                serialized.FindProperty("handle").objectReferenceValue = handle;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        ScrollRect scrollRect = listFrame.GetComponent<ScrollRect>();
        LoadGamePanelView panelView = panel.GetComponent<LoadGamePanelView>();
        if (panelView != null && scrollRect != null)
        {
            var serialized = new SerializedObject(panelView);
            serialized.FindProperty("scrollRect").objectReferenceValue = scrollRect;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, MenuScenePath);
        Debug.Log("[MainMenuUIBuilder] Load Game circular scroll thumb fixed.");
    }

    [MenuItem("TCG Card Chaos/UI/Build Main Menu UI")]
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

        LoadGamePanelView loadPanel = BuildLoadGamePanel(canvas.transform, font);
        AssignLoadGamePanel(view, loadPanel);

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
        EnableAutoSize(label, 20f, 40f);
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
        EnableAutoSize(text, 13f, 26f);
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

    static void AssignLoadGamePanel(MainMenuView view, LoadGamePanelView panel)
    {
        if (view == null || panel == null)
            return;

        var serialized = new SerializedObject(view);
        serialized.FindProperty("loadGamePanel").objectReferenceValue = panel;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static LoadGamePanelView CreateLoadGamePanel(Transform parent)
    {
        EnsureLocalizationTable();
        EnsureLoadGameConfirmPlaceholders();
        return BuildLoadGamePanel(parent, FindPreferredFont());
    }

    static LoadGamePanelView BuildLoadGamePanel(Transform parent, TMP_FontAsset font)
    {
        EnsureFolder("Assets/UI");
        EnsureFolder("Assets/UI/LoadGame");
        EnsureFolder(LoadGameArtFolder);

        Sprite listFrameSprite = LoadGameSprite("panel_1.png");
        Sprite slotSprite = LoadGameSprite("panel_2.png");
        Sprite scrollBarSprite = LoadGameSprite("scroll_bar.png");
        Sprite scrollHandleSprite = LoadGameSprite("scroll_circle_icon.png");
        Sprite cancelSprite = LoadGameSprite("cancel_button.png");
        Texture thumbnailTexture = LoadGameTexture("image_load_game.png");

        RectTransform root = CreateUIObject("Panel_LoadGame", parent);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();

        var panelView = root.gameObject.AddComponent<LoadGamePanelView>();

        EnsureLoadGameBackground(root);

        RectTransform titleRect = CreateUIObject("Title", root);
        SetCenterAnchored(titleRect, new Vector2(0f, 430f), new Vector2(720f, 88f));
        TMP_Text title = CreateText(titleRect, font, 64f, TextAlignmentOptions.Center, Color.white);
        title.fontStyle = FontStyles.Bold;
        ApplyTextOutline(title, 0.28f, Color.black);
        EnableAutoSize(title, 32f, 64f);
        AddLocalizedText(titleRect.gameObject, LocalizationKeys.LoadGameTitle);

        RectTransform listFrame = CreateUIObject("ListFrame", root);
        SetCenterAnchored(listFrame, new Vector2(0f, 20f), new Vector2(1320f, 640f));
        var listImage = listFrame.gameObject.AddComponent<Image>();
        listImage.sprite = listFrameSprite;
        listImage.type = Image.Type.Sliced;
        listImage.color = Color.white;

        RectTransform viewport = CreateUIObject("Viewport", listFrame);
        StretchToParent(viewport, new Vector4(28f, 28f, 56f, 28f));
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateUIObject("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 18f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LoadGameSlotView slot1 = BuildLoadGameSlot(content, "Slot_1", font, slotSprite, thumbnailTexture);
        LoadGameSlotView slot2 = BuildLoadGameSlot(content, "Slot_2", font, slotSprite, thumbnailTexture);
        LoadGameSlotView slot3 = BuildLoadGameSlot(content, "Slot_3", font, slotSprite, thumbnailTexture);

        RectTransform scrollbarRect = CreateUIObject("Scrollbar", listFrame);
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.anchoredPosition = new Vector2(-18f, 0f);
        scrollbarRect.sizeDelta = new Vector2(22f, -72f);

        var track = scrollbarRect.gameObject.AddComponent<Image>();
        track.sprite = scrollBarSprite;
        track.color = Color.white;

        var scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        RectTransform slidingArea = CreateUIObject("Sliding Area", scrollbarRect);
        StretchToParent(slidingArea, new Vector4(0f, 18f, 0f, 18f));

        RectTransform handle = CreateUIObject("Handle", slidingArea);
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(1f, 0f);
        handle.pivot = new Vector2(0.5f, 0.5f);
        handle.anchoredPosition = Vector2.zero;
        handle.sizeDelta = new Vector2(0f, 40f);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = scrollHandleSprite;
        handleImage.color = Color.white;
        handleImage.preserveAspect = true;
        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handle;
        scrollbar.gameObject.AddComponent<LoadGameCircularScrollThumb>();

        var scrollRect = listFrame.gameObject.AddComponent<ScrollRect>();
        scrollRect.content = content;
        scrollRect.viewport = viewport;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        Button cancel = BuildLoadGameCancelButton(root, cancelSprite, font);

        LoadGameConfirmView confirm = BuildLoadGameConfirm(root, font);
        EnsureLoadGameDrawOrder(root);

        var panelSerialized = new SerializedObject(panelView);
        panelSerialized.FindProperty("root").objectReferenceValue = root.gameObject;
        panelSerialized.FindProperty("cancelButton").objectReferenceValue = cancel;
        panelSerialized.FindProperty("scrollRect").objectReferenceValue = scrollRect;
        panelSerialized.FindProperty("confirmView").objectReferenceValue = confirm;
        SerializedProperty slotsProp = panelSerialized.FindProperty("slots");
        slotsProp.arraySize = 3;
        slotsProp.GetArrayElementAtIndex(0).objectReferenceValue = slot1;
        slotsProp.GetArrayElementAtIndex(1).objectReferenceValue = slot2;
        slotsProp.GetArrayElementAtIndex(2).objectReferenceValue = slot3;
        panelSerialized.ApplyModifiedPropertiesWithoutUndo();

        root.gameObject.SetActive(false);
        return panelView;
    }

    static void AssignLoadGameConfirm(LoadGamePanelView panel, LoadGameConfirmView confirm)
    {
        if (panel == null || confirm == null)
            return;

        var serialized = new SerializedObject(panel);
        serialized.FindProperty("confirmView").objectReferenceValue = confirm;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static LoadGameConfirmView BuildLoadGameConfirm(Transform parent, TMP_FontAsset font)
    {
        EnsureFolder("Assets/UI");
        EnsureFolder("Assets/UI/LoadGame");
        EnsureFolder(LoadGameArtFolder);
        EnsureLoadGameConfirmPlaceholders();

        RectTransform root = CreateUIObject("Panel_LoadConfirm", parent);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();

        var confirmView = root.gameObject.AddComponent<LoadGameConfirmView>();

        // Full-screen blocker so Load Game list cannot be clicked underneath.
        RectTransform dimmer = CreateUIObject("Dimmer", root);
        StretchToParent(dimmer, Vector4.zero);
        var dimmerImage = dimmer.gameObject.AddComponent<Image>();
        dimmerImage.color = new Color(0.02f, 0.04f, 0.10f, 0.82f);
        dimmerImage.raycastTarget = true;

        // Mock: wide navy band across the middle (~1/3 of 1080).
        RectTransform band = CreateUIObject("Band", root);
        band.anchorMin = new Vector2(0f, 0.5f);
        band.anchorMax = new Vector2(1f, 0.5f);
        band.pivot = new Vector2(0.5f, 0.5f);
        band.anchoredPosition = Vector2.zero;
        band.sizeDelta = new Vector2(0f, 395f);
        band.gameObject.AddComponent<Image>();
        ApplySolidColorImage(band.GetComponent<Image>(), new Color(0.12f, 0.22f, 0.42f, 1f));

        CompleteLoadGameConfirm(root, font);
        root.gameObject.SetActive(false);
        return confirmView;
    }

    static LoadGameConfirmView CompleteLoadGameConfirm(Transform confirmRoot, TMP_FontAsset font)
    {
        EnsureLoadGameConfirmPlaceholders();

        var confirmView = confirmRoot.GetComponent<LoadGameConfirmView>();
        if (confirmView == null)
            confirmView = confirmRoot.gameObject.AddComponent<LoadGameConfirmView>();

        Transform band = confirmRoot.Find("Band");
        if (band == null)
        {
            RectTransform bandRect = CreateUIObject("Band", confirmRoot);
            bandRect.anchorMin = new Vector2(0f, 0.5f);
            bandRect.anchorMax = new Vector2(1f, 0.5f);
            bandRect.pivot = new Vector2(0.5f, 0.5f);
            bandRect.anchoredPosition = Vector2.zero;
            bandRect.sizeDelta = new Vector2(0f, 395f);
            bandRect.gameObject.AddComponent<Image>();
            band = bandRect;
        }

        ApplySolidColorImage(
            band.GetComponent<Image>(),
            new Color(0.12f, 0.22f, 0.42f, 1f));

        Transform buttonRow = band.Find("ButtonRow");
        if (buttonRow == null)
        {
            RectTransform row = CreateUIObject("ButtonRow", band);
            SetCenterAnchored(row, new Vector2(0f, -82f), new Vector2(560f, 95f));
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.spacing = 72f;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            buttonRow = row;
        }

        if (buttonRow.Find("Button_Yes") == null)
        {
            BuildConfirmChoiceButton(
                buttonRow,
                "Button_Yes",
                LoadGameSprite("yes_button.png"),
                new Color(0.45f, 0.85f, 0.20f, 1f),
                LocalizationKeys.LoadGameConfirmYes,
                "Yes");
        }

        if (buttonRow.Find("Button_No") == null)
        {
            BuildConfirmChoiceButton(
                buttonRow,
                "Button_No",
                LoadGameSprite("no_button.png"),
                new Color(0.95f, 0.42f, 0.12f, 1f),
                LocalizationKeys.LoadGameConfirmNo,
                "No");
        }

        Transform message = band.Find("Message");
        if (message == null)
        {
            RectTransform messageRect = CreateUIObject("Message", band);
            SetCenterAnchored(messageRect, new Vector2(0f, 92f), new Vector2(1500f, 90f));
            message = messageRect;
        }

        StyleConfirmLabel(
            message.gameObject,
            font,
            52f,
            LocalizationKeys.LoadGameConfirmMessage,
            "Are you sure you want to load this game?");

        WireLoadGameConfirmReferences(confirmView, confirmRoot);
        confirmRoot.gameObject.SetActive(false);
        return confirmView;
    }

    static void StyleConfirmLabel(
        GameObject target,
        TMP_FontAsset font,
        float fontSize,
        string localizationKey,
        string fallbackText)
    {
        TMP_Text label = target.GetComponent<TMP_Text>();
        if (label == null)
            label = CreateText(target.GetComponent<RectTransform>(), font, fontSize, TextAlignmentOptions.Center, Color.white);

        label.color = Color.white;
        label.raycastTarget = false;
        label.text = fallbackText;

        if (target.GetComponent<LocalizedText>() == null)
        {
            try
            {
                AddLocalizedText(target, localizationKey);
            }
            catch (System.Exception)
            {
            }
        }
    }

    static void CopyExistingMenuTextMaterial(TMP_Text target)
    {
        if (target == null)
            return;

        Transform loadPanel = target.transform;
        while (loadPanel != null && loadPanel.name != "Panel_LoadGame")
            loadPanel = loadPanel.parent;

        TMP_Text title = loadPanel != null
            ? loadPanel.Find("Title")?.GetComponent<TMP_Text>()
            : null;
        if (title == null || title.fontSharedMaterial == null)
            return;

        target.font = title.font;
        target.fontSharedMaterial = title.fontSharedMaterial;
    }

    static bool IsLoadGameConfirmComplete(Transform confirmRoot)
    {
        if (confirmRoot == null)
            return false;

        Transform band = confirmRoot.Find("Band");
        if (band == null)
            return false;

        if (band.Find("Message") == null)
            return false;

        Transform buttonRow = band.Find("ButtonRow");
        if (buttonRow == null)
            return false;

        return buttonRow.Find("Button_Yes") != null && buttonRow.Find("Button_No") != null;
    }

    static void WireLoadGameConfirmReferences(LoadGameConfirmView confirm, Transform confirmRoot)
    {
        if (confirm == null || confirmRoot == null)
            return;

        Transform band = confirmRoot.Find("Band");
        Button yes = band != null ? band.Find("ButtonRow/Button_Yes")?.GetComponent<Button>() : null;
        Button no = band != null ? band.Find("ButtonRow/Button_No")?.GetComponent<Button>() : null;

        var serialized = new SerializedObject(confirm);
        serialized.FindProperty("root").objectReferenceValue = confirmRoot.gameObject;
        serialized.FindProperty("yesButton").objectReferenceValue = yes;
        serialized.FindProperty("noButton").objectReferenceValue = no;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureLoadGameBackground(Transform loadPanel)
    {
        if (loadPanel == null)
            return;

        Transform backgroundTransform = loadPanel.Find("Background");
        Transform dimmer = loadPanel.Find("Dimmer");
        if (backgroundTransform == null && dimmer != null)
        {
            dimmer.gameObject.name = "Background";
            backgroundTransform = dimmer;
        }

        if (backgroundTransform == null)
        {
            RectTransform created = CreateUIObject("Background", loadPanel);
            StretchToParent(created, Vector4.zero);
            created.gameObject.AddComponent<Image>();
            backgroundTransform = created;
        }

        RectTransform rect = backgroundTransform as RectTransform;
        if (rect != null)
            StretchToParent(rect, Vector4.zero);

        Image image = backgroundTransform.GetComponent<Image>();
        if (image == null)
            image = backgroundTransform.gameObject.AddComponent<Image>();

        ApplySolidColorImage(image, new Color(0.039f, 0.047f, 0.114f, 1f));

        backgroundTransform.SetAsFirstSibling();
    }

    static void ApplySolidColorImage(Image image, Color defaultColor)
    {
        if (image == null)
            return;

        bool stillUsingSprite = image.sprite != null;
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = true;
        if (stillUsingSprite || image.color == Color.white)
            image.color = defaultColor;
    }

    static void EnsureLoadGameDrawOrder(Transform loadPanel)
    {
        if (loadPanel == null)
            return;

        Transform background = loadPanel.Find("Background") ?? loadPanel.Find("Dimmer");
        if (background != null)
            background.SetAsFirstSibling();

        Transform cancel = loadPanel.Find("Button_Cancel");
        Transform confirm = loadPanel.Find("Panel_LoadConfirm");
        if (confirm != null)
            confirm.SetAsLastSibling();

        if (cancel != null && confirm != null)
        {
            int confirmIndex = confirm.GetSiblingIndex();
            cancel.SetSiblingIndex(confirmIndex);
            confirm.SetAsLastSibling();
        }
        else if (cancel != null)
        {
            cancel.SetAsLastSibling();
        }
    }

    static Button BuildConfirmChoiceButton(
        Transform parent,
        string objectName,
        Sprite sprite,
        Color fallbackTint,
        string localizationKey,
        string fallbackText)
    {
        RectTransform rect = CreateUIObject(objectName, parent);
        var layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 244f;
        layout.preferredHeight = 95f;
        layout.minWidth = 244f;
        layout.minHeight = 95f;
        rect.sizeDelta = new Vector2(244f, 95f);

        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = sprite != null ? Color.white : fallbackTint;
        image.preserveAspect = true;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        button.colors = colors;

        RectTransform labelRect = CreateUIObject("Label", rect);
        StretchToParent(labelRect, new Vector4(12f, 8f, 12f, 8f));
        StyleConfirmLabel(labelRect.gameObject, FindPreferredFont(), 38f, localizationKey, fallbackText);

        return button;
    }

    static void EnsureLoadGameConfirmPlaceholders()
    {
        EnsureFolder(LoadGameArtFolder);

        if (LoadGameSprite("confirm_band.png") == null && LoadGameSprite("panel_3.png") != null)
        {
            File.Copy(
                LoadGameArtFolder + "/panel_3.png",
                LoadGameArtFolder + "/confirm_band.png",
                overwrite: true);
            AssetDatabase.ImportAsset(LoadGameArtFolder + "/confirm_band.png", ImportAssetOptions.ForceUpdate);
        }

        if (LoadGameSprite("confirm_band.png") == null)
        {
            Texture2D band = CreateSolidRoundedTexture(
                64,
                64,
                8,
                new Color(0.12f, 0.22f, 0.42f, 1f));
            EnsureLoadGameUiSprite("confirm_band.png", band, 8f);
        }

        if (LoadGameSprite("yes_button.png") == null)
        {
            Texture2D yes = CreateSolidRoundedTexture(
                64,
                64,
                14,
                new Color(0.45f, 0.85f, 0.20f, 1f));
            EnsureLoadGameUiSprite("yes_button.png", yes, 16f);
        }

        if (LoadGameSprite("no_button.png") == null)
        {
            Texture2D no = CreateSolidRoundedTexture(
                64,
                64,
                14,
                new Color(0.95f, 0.42f, 0.12f, 1f));
            EnsureLoadGameUiSprite("no_button.png", no, 16f);
        }
    }

    static Sprite EnsureLoadGameUiSprite(string fileName, Texture2D texture, float sliceBorder)
    {
        string assetPath = LoadGameArtFolder + "/" + fileName;
        File.WriteAllBytes(assetPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        if (sliceBorder > 0f)
            importer.spriteBorder = new Vector4(sliceBorder, sliceBorder, sliceBorder, sliceBorder);

        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    static Texture2D CreateSolidRoundedTexture(int width, int height, int radius, Color fill)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(
                    x,
                    y,
                    IsInsideRoundedRect(x + 0.5f, y + 0.5f, width, height, radius) ? fill : Color.clear);
            }
        }

        texture.Apply();
        return texture;
    }

    static LoadGameSlotView BuildLoadGameSlot(
        Transform parent,
        string objectName,
        TMP_FontAsset font,
        Sprite slotSprite,
        Texture thumbnailTexture)
    {
        RectTransform slot = CreateUIObject(objectName, parent);
        var layoutElement = slot.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 300f;
        layoutElement.minHeight = 300f;

        var background = slot.gameObject.AddComponent<Image>();
        background.sprite = slotSprite;
        background.type = Image.Type.Sliced;
        background.color = Color.white;

        var button = slot.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        var colors = button.colors;
        colors.highlightedColor = new Color(0.66f, 0.70f, 0.88f, 1f);
        colors.pressedColor = new Color(0.50f, 0.54f, 0.72f, 1f);
        button.colors = colors;

        var slotView = slot.gameObject.AddComponent<LoadGameSlotView>();

        RectTransform thumb = CreateUIObject("Thumbnail", slot);
        thumb.anchorMin = new Vector2(0f, 0.5f);
        thumb.anchorMax = new Vector2(0f, 0.5f);
        thumb.pivot = new Vector2(0f, 0.5f);
        thumb.anchoredPosition = new Vector2(18f, 0f);
        thumb.sizeDelta = new Vector2(156f, 156f);
        var thumbImage = thumb.gameObject.AddComponent<RawImage>();
        thumbImage.texture = thumbnailTexture;
        thumbImage.color = Color.white;
        thumbImage.raycastTarget = false;

        RectTransform saveNameRect = CreateUIObject("SaveName", slot);
        saveNameRect.anchorMin = new Vector2(1f, 1f);
        saveNameRect.anchorMax = new Vector2(1f, 1f);
        saveNameRect.pivot = new Vector2(1f, 1f);
        saveNameRect.anchoredPosition = new Vector2(-22f, -14f);
        saveNameRect.sizeDelta = new Vector2(280f, 40f);
        TMP_Text saveName = CreateText(saveNameRect, font, 26f, TextAlignmentOptions.Right,
            new Color(0.10f, 0.12f, 0.16f));
        saveName.raycastTarget = false;
        EnableAutoSize(saveName, 16f, 26f);
        saveName.text = "Auto Save 1";

        RectTransform stats = CreateUIObject("Stats", slot);
        stats.anchorMin = new Vector2(0f, 0.5f);
        stats.anchorMax = new Vector2(1f, 0.5f);
        stats.pivot = new Vector2(0f, 0.5f);
        stats.anchoredPosition = new Vector2(192f, -8f);
        stats.sizeDelta = new Vector2(-220f, 140f);

        var statsLayout = stats.gameObject.AddComponent<VerticalLayoutGroup>();
        statsLayout.childAlignment = TextAnchor.MiddleLeft;
        statsLayout.spacing = 4f;
        statsLayout.childControlWidth = true;
        statsLayout.childControlHeight = true;
        statsLayout.childForceExpandWidth = true;
        statsLayout.childForceExpandHeight = false;

        TMP_Text dateValue = BuildStatRow(stats, "Date", font, LocalizationKeys.LoadGameDate);
        TMP_Text playTimeValue = BuildStatRow(stats, "PlayTime", font, LocalizationKeys.LoadGamePlayTime);
        TMP_Text cardsValue = BuildStatRow(stats, "Cards", font, LocalizationKeys.LoadGameCardsPlaced);
        TMP_Text shelvesValue = BuildStatRow(stats, "Shelves", font, LocalizationKeys.LoadGameShelves);

        var slotSerialized = new SerializedObject(slotView);
        slotSerialized.FindProperty("selectButton").objectReferenceValue = button;
        slotSerialized.FindProperty("thumbnail").objectReferenceValue = thumbImage;
        slotSerialized.FindProperty("saveNameText").objectReferenceValue = saveName;
        slotSerialized.FindProperty("dateValueText").objectReferenceValue = dateValue;
        slotSerialized.FindProperty("playTimeValueText").objectReferenceValue = playTimeValue;
        slotSerialized.FindProperty("cardsValueText").objectReferenceValue = cardsValue;
        slotSerialized.FindProperty("shelvesValueText").objectReferenceValue = shelvesValue;
        slotSerialized.ApplyModifiedPropertiesWithoutUndo();

        return slotView;
    }

    static TMP_Text BuildStatRow(Transform parent, string rowName, TMP_FontAsset font, string labelKey)
    {
        RectTransform row = CreateUIObject("Row_" + rowName, parent);
        var rowElement = row.gameObject.AddComponent<LayoutElement>();
        rowElement.preferredHeight = 30f;
        rowElement.minHeight = 30f;

        var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.spacing = 10f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        RectTransform labelRect = CreateUIObject("Label", row);
        var labelElement = labelRect.gameObject.AddComponent<LayoutElement>();
        labelElement.preferredWidth = 210f;
        labelElement.minWidth = 140f;
        TMP_Text label = CreateText(labelRect, font, 22f, TextAlignmentOptions.Left,
            new Color(0.12f, 0.13f, 0.16f));
        label.raycastTarget = false;
        EnableAutoSize(label, 14f, 22f);
        AddLocalizedText(labelRect.gameObject, labelKey);

        RectTransform valueRect = CreateUIObject("Value", row);
        var valueElement = valueRect.gameObject.AddComponent<LayoutElement>();
        valueElement.preferredWidth = 420f;
        valueElement.flexibleWidth = 1f;
        TMP_Text value = CreateText(valueRect, font, 22f, TextAlignmentOptions.Left, Color.white);
        value.raycastTarget = false;
        EnableAutoSize(value, 14f, 22f);
        value.text = "—";
        return value;
    }

    static Button BuildLoadGameCancelButton(Transform parent, Sprite cancelSprite, TMP_FontAsset font)
    {
        RectTransform rect = CreateUIObject("Button_Cancel", parent);
        SetCornerAnchored(rect, new Vector2(1f, 0f), new Vector2(-210f, 78f), new Vector2(220f, 78f));

        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = cancelSprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        RectTransform labelRect = CreateUIObject("Label", rect);
        StretchToParent(labelRect, new Vector4(14f, 8f, 14f, 10f));
        TMP_Text label = CreateText(labelRect, font, 36f, TextAlignmentOptions.Center, Color.white);
        label.raycastTarget = false;
        EnableAutoSize(label, 18f, 36f);
        AddLocalizedText(labelRect.gameObject, LocalizationKeys.LoadGameCancel);
        CopyExistingMenuTextMaterial(label);
        return button;
    }

    static void EnsureLoadGameCancelLabel(Transform loadPanel)
    {
        if (loadPanel == null)
            return;

        Transform cancel = loadPanel.Find("Button_Cancel");
        if (cancel == null || cancel.GetComponentInChildren<TMP_Text>(true) != null)
            return;

        RectTransform labelRect = CreateUIObject("Label", cancel);
        StretchToParent(labelRect, new Vector4(14f, 8f, 14f, 10f));
        TMP_Text label = CreateText(labelRect, FindPreferredFont(), 36f, TextAlignmentOptions.Center, Color.white);
        label.raycastTarget = false;
        EnableAutoSize(label, 18f, 36f);
        AddLocalizedText(labelRect.gameObject, LocalizationKeys.LoadGameCancel);
        CopyExistingMenuTextMaterial(label);
    }

    static Sprite LoadGameSprite(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(LoadGameArtFolder + "/" + fileName);
    }

    static Texture LoadGameTexture(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Texture>(LoadGameArtFolder + "/" + fileName);
    }

    static Sprite EnsureUiSprite(string fileName, Texture2D texture, float sliceBorder)
    {
        string assetPath = UiArtFolder + "/" + fileName;
        File.WriteAllBytes(assetPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        if (sliceBorder > 0f)
        {
            importer.spriteBorder = new Vector4(sliceBorder, sliceBorder, sliceBorder, sliceBorder);
        }

        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    static Texture2D CreateRoundedPlaceholderTexture(int radius)
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, IsInsideRoundedRect(x + 0.5f, y + 0.5f, size, size, radius)
                    ? Color.white
                    : Color.clear);
            }
        }

        texture.Apply();
        return texture;
    }

    static Texture2D CreateCirclePlaceholderTexture()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        float center = size * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - center;
                float dy = y + 0.5f - center;
                texture.SetPixel(x, y, dx * dx + dy * dy <= radius * radius ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        return texture;
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

        EnsureTmpFont(text, font);
        return text;
    }

    static void EnsureTmpFont(TMP_Text text, TMP_FontAsset preferredFont = null)
    {
        if (text == null)
            return;

        TMP_FontAsset font = preferredFont;
        if (font == null || font.name.IndexOf("Baloo", System.StringComparison.OrdinalIgnoreCase) < 0)
            font = FindPreferredFont();

        if (font != null)
            text.font = font;
    }

    static void ApplyTextOutline(TMP_Text text, float width, Color color)
    {
        if (text == null)
            return;

        EnsureTmpFont(text);
        if (text.font == null)
            return;

        try
        {
            text.ForceMeshUpdate(true);
            if (text.fontSharedMaterial == null)
                return;

            text.outlineWidth = width;
            text.outlineColor = color;
        }
        catch (System.Exception)
        {
            // Some TMP materials are not outline-ready during editor build; text still renders.
        }
    }

    /// <summary>
    /// Auto sizing keeps long translations inside the authored art. Short copy stays at
    /// <paramref name="max"/>; overflow is truncated after shrinking.
    /// </summary>
    static void EnableAutoSize(TMP_Text text, float min, float max)
    {
        text.fontSize = max;
        text.enableAutoSizing = true;
        text.fontSizeMin = min;
        text.fontSizeMax = max;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
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

    [MenuItem("TCG Card Chaos/UI/Create Or Update Localization Table")]
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
        table.EditorEnsureKey(LocalizationKeys.LoadGameTitle, "Load Game", "Oyun Yükle");
        table.EditorEnsureKey(LocalizationKeys.LoadGameCancel, "Cancel", "İptal");
        table.EditorEnsureKey(LocalizationKeys.LoadGameDate, "Date:", "Tarih:");
        table.EditorEnsureKey(LocalizationKeys.LoadGamePlayTime, "Play Time:", "Oynama Süresi:");
        table.EditorEnsureKey(LocalizationKeys.LoadGameCardsPlaced, "Cards Placed:", "Yerleştirilen Kartlar:");
        table.EditorEnsureKey(LocalizationKeys.LoadGameShelves, "Shelves:", "Raflar:");
        table.EditorEnsureKey(
            LocalizationKeys.LoadGameConfirmMessage,
            "Are you sure you want to load this game?",
            "Bu oyunu yüklemek istediğine emin misin?");
        table.EditorEnsureKey(LocalizationKeys.LoadGameConfirmYes, "Yes", "Evet");
        table.EditorEnsureKey(LocalizationKeys.LoadGameConfirmNo, "No", "Hayır");
        table.EditorEnsureKey(LocalizationKeys.PauseBack, "Back", "Geri");
        table.EditorEnsureKey(LocalizationKeys.PauseResume, "Resume", "Devam");
        table.EditorEnsureKey(LocalizationKeys.PauseSave, "Save Game", "Oyunu Kaydet");
        table.EditorEnsureKey(LocalizationKeys.SaveGameTitle, "Save Game", "Oyunu Kaydet");
        table.EditorEnsureKey(LocalizationKeys.SaveGameEmptySlot, "Empty Slot", "Boş Slot");
        table.EditorEnsureKey(LocalizationKeys.SaveGameNotAvailable, "N/A", "N/A");
        table.EditorEnsureKey(LocalizationKeys.SaveGameDeleteHint, "Delete", "Sil");
        table.EditorEnsureKey(
            LocalizationKeys.SaveGameOverwriteConfirm,
            "Are you sure you want to overwrite this save?",
            "Bu kaydın üzerine yazmak istediğine emin misin?");
        table.EditorEnsureKey(LocalizationKeys.SaveGameSaving, "Saving", "Kaydediliyor");
        table.EditorEnsureKey(LocalizationKeys.SaveGameSaved, "Saved", "Kaydedildi");
        table.EditorEnsureKey(LocalizationKeys.UiLoading, "Loading", "Yükleniyor");
        table.EditorEnsureKey(
            LocalizationKeys.UiLoadingDisclaimer,
            "This game is a work of fiction.\nAll locations, cards, and packs in the game are entirely imaginary\nand have no connection to any real places or works.",
            "Bu oyun tamamen kurgusal bir eserdir.\nOyunda yer alan tüm mekanlar, kartlar, paketler tamamen hayal ürünüdür\nve gerçek yerler veya eserlerle hiçbir bağlantısı yoktur.");

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

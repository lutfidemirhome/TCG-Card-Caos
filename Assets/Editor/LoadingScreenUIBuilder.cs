using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds / refreshes the authored loading overlay in MenuScene and saves the
/// Resources prefab used when a save is loaded from inside the game.
/// Menu: TCG Card Caos → UI → Add Loading Screen
/// </summary>
public static class LoadingScreenUIBuilder
{
    const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
    const string GameScenePath = "Assets/Scenes/MainScene.unity";
    const string ArtFolder = "Assets/UI/Loading/Art";
    const string MenuArtFolder = "Assets/UI/MainMenu/Art";
    const string PrefabPath = "Assets/Resources/UI/LoadingScreen.prefab";
    const string CanvasRootName = "LoadingCanvas";

    [MenuItem("TCG Card Caos/UI/Add Loading Screen")]
    public static void AddLoadingScreen()
    {
        if (!File.Exists(MenuScenePath))
        {
            EditorUtility.DisplayDialog("Loading Screen", "MenuScene not found.", "OK");
            return;
        }

        MainMenuUIBuilder.EnsureLocalizationTable();
        EnsureFolders();
        ConfigureDroppedSprites();

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != MenuScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        }

        Transform canvas = FindLoadingCanvas();
        if (canvas == null)
            canvas = BuildCanvas().transform;

        AssignArt(canvas);
        ApplyLayout(canvas);
        BindView(canvas);

        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/UI");
        PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, PrefabPath);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, MenuScenePath);

        SyncToGameScene();

        Selection.activeGameObject = canvas.gameObject;
        EditorGUIUtility.PingObject(canvas.gameObject);
        Debug.Log("[LoadingScreenUIBuilder] LoadingCanvas is in MenuScene and MainScene. Runtime uses Resources/UI/LoadingScreen.");
    }

    static void SyncToGameScene()
    {
        if (!File.Exists(GameScenePath))
            return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        UnityEngine.SceneManagement.Scene gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        Transform existing = FindLoadingCanvas();
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = CanvasRootName;
        var view = instance.GetComponent<LoadingScreenUI>();
        if (view != null)
        {
            var serialized = new SerializedObject(view);
            serialized.FindProperty("editorPreview").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(gameScene);
        EditorSceneManager.SaveScene(gameScene, GameScenePath);
        EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
    }

    static Transform FindLoadingCanvas()
    {
        GameObject found = GameObject.Find(CanvasRootName);
        if (found != null)
            return found.transform;

        LoadingScreenUI[] views = Object.FindObjectsByType<LoadingScreenUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return views.Length > 0 ? views[0].transform : null;
    }

    static Canvas BuildCanvas()
    {
        var canvasGo = new GameObject(CanvasRootName);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
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
        canvasGo.AddComponent<LoadingScreenUI>();

        RectTransform panel = CreateUIObject("Panel_Loading", canvasGo.transform);
        Stretch(panel);

        RectTransform background = CreateUIObject("Background", panel);
        Stretch(background);
        var backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.08f, 0.09f, 0.16f, 1f);
        backgroundImage.raycastTarget = true;

        RectTransform tint = CreateUIObject("Tint", panel);
        Stretch(tint);
        var tintImage = tint.gameObject.AddComponent<Image>();
        tintImage.color = new Color(0.04f, 0.05f, 0.16f, 0.45f);
        tintImage.raycastTarget = false;

        RectTransform logo = CreateUIObject("Logo", panel);
        SetCenter(logo, new Vector2(0f, 110f), new Vector2(560f, 400f));
        var logoImage = logo.gameObject.AddComponent<Image>();
        logoImage.sprite = LoadSprite(MenuArtFolder + "/tcg_demo_logo.png");
        logoImage.preserveAspect = true;
        logoImage.color = Color.white;
        logoImage.raycastTarget = false;

        RectTransform spinner = CreateUIObject("Spinner", panel);
        SetBottom(spinner, 188f, new Vector2(80f, 80f));

        RectTransform spinnerBase = CreateUIObject("SpinnerBase", spinner);
        Stretch(spinnerBase);
        var baseImage = spinnerBase.gameObject.AddComponent<Image>();
        baseImage.preserveAspect = true;
        baseImage.color = Color.white;
        baseImage.raycastTarget = false;

        RectTransform spinnerYellow = CreateUIObject("SpinnerYellow", spinner);
        Stretch(spinnerYellow);
        var yellowImage = spinnerYellow.gameObject.AddComponent<Image>();
        yellowImage.preserveAspect = true;
        yellowImage.color = Color.white;
        yellowImage.raycastTarget = false;

        RectTransform disclaimerRect = CreateUIObject("Disclaimer", panel);
        SetCenter(disclaimerRect, new Vector2(0f, -102f), new Vector2(1720f, 118f));
        disclaimerRect.pivot = new Vector2(0.5f, 1f);
        var disclaimer = disclaimerRect.gameObject.AddComponent<TextMeshProUGUI>();
        disclaimer.fontSize = 26f;
        disclaimer.enableAutoSizing = false;
        disclaimer.alignment = TextAlignmentOptions.Center;
        disclaimer.overflowMode = TextOverflowModes.Overflow;
        disclaimer.textWrappingMode = TextWrappingModes.Normal;
        disclaimer.color = Color.white;
        disclaimer.raycastTarget = false;
        disclaimer.text = "This game is a work of fiction.\nAll locations, cards, and packs in the game are entirely imaginary\nand have no connection to any real places or works.";
        TMP_FontAsset disclaimerFont = FindPreferredFont();
        if (disclaimerFont != null)
        {
            disclaimer.font = disclaimerFont;
            disclaimer.fontSharedMaterial = disclaimerFont.material;
        }
        disclaimerRect.gameObject.SetActive(false);

        RectTransform labelRect = CreateUIObject("Label", panel);
        SetBottom(labelRect, 96f, new Vector2(480f, 52f));
        var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = 36f;
        label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.Center;
        label.overflowMode = TextOverflowModes.Overflow;
        label.color = new Color(0.92f, 0.92f, 0.92f, 1f);
        label.raycastTarget = false;
        label.text = "Loading";
        TMP_FontAsset font = FindPreferredFont();
        if (font != null)
        {
            label.font = font;
            label.fontSharedMaterial = font.material;
        }

        panel.gameObject.SetActive(false);
        return canvas;
    }

    static void AssignArt(Transform canvas)
    {
        Transform panel = canvas.Find("Panel_Loading");
        if (panel == null)
            return;

        Sprite bg = FindArtSprite("bg");
        Sprite yellow = FindYellowSpinnerSprite();
        Sprite ring = FindBaseSpinnerSprite(yellow);

        Image background = FindImage(panel, "Background");
        if (background != null)
        {
            background.sprite = bg;
            background.preserveAspect = false;
            background.color = bg != null ? Color.white : new Color(0.08f, 0.09f, 0.16f, 1f);
        }

        Image logo = FindImage(panel, "Logo");
        if (logo != null)
        {
            logo.sprite = LoadSprite(MenuArtFolder + "/tcg_demo_logo.png");
            logo.preserveAspect = true;
        }

        Image baseImage = FindImage(panel, "Spinner/SpinnerBase");
        if (baseImage != null)
        {
            baseImage.sprite = ring;
            baseImage.preserveAspect = true;
        }

        Image yellowImage = FindImage(panel, "Spinner/SpinnerYellow");
        if (yellowImage != null)
        {
            yellowImage.sprite = yellow;
            yellowImage.preserveAspect = true;
        }
    }

    static void ApplyLayout(Transform canvas)
    {
        Transform panel = canvas.Find("Panel_Loading");
        if (panel == null)
            return;

        Transform spinner = panel.Find("Spinner");
        if (spinner is RectTransform spinnerRect)
            SetBottom(spinnerRect, 188f, new Vector2(80f, 80f));

        Transform disclaimer = panel.Find("Disclaimer");
        if (disclaimer is RectTransform disclaimerRect)
        {
            SetCenter(disclaimerRect, new Vector2(0f, -102f), new Vector2(1720f, 118f));
            disclaimerRect.pivot = new Vector2(0.5f, 1f);
        }

        Transform label = panel.Find("Label");
        if (label is RectTransform labelRect)
            SetBottom(labelRect, 96f, new Vector2(480f, 52f));
    }

    static void BindView(Transform canvas)
    {
        var view = canvas.GetComponent<LoadingScreenUI>();
        if (view == null)
            view = canvas.gameObject.AddComponent<LoadingScreenUI>();

        Transform panel = canvas.Find("Panel_Loading");
        Transform yellow = canvas.Find("Panel_Loading/Spinner/SpinnerYellow");
        Transform label = canvas.Find("Panel_Loading/Label");
        Transform disclaimer = canvas.Find("Panel_Loading/Disclaimer");
        TMP_Text labelText = label != null ? label.GetComponent<TMP_Text>() : null;
        TMP_Text disclaimerText = disclaimer != null ? disclaimer.GetComponent<TMP_Text>() : null;
        if (labelText != null)
        {
            labelText.text = "Loading";
            labelText.color = Color.white;
        }

        var serialized = new SerializedObject(view);
        serialized.FindProperty("root").objectReferenceValue = panel != null ? panel.gameObject : null;
        serialized.FindProperty("spinnerYellow").objectReferenceValue = yellow as RectTransform;
        serialized.FindProperty("label").objectReferenceValue = labelText;
        serialized.FindProperty("disclaimer").objectReferenceValue = disclaimerText;
        serialized.FindProperty("editorPreview").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static Sprite FindArtSprite(string nameContains)
    {
        if (!Directory.Exists(ArtFolder))
            return null;

        string[] files = Directory.GetFiles(ArtFolder, "*.png");
        for (int i = 0; i < files.Length; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(files[i]);
            if (fileName.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            return LoadSprite(ToAssetPath(files[i]));
        }

        return null;
    }

    static Sprite FindYellowSpinnerSprite()
    {
        if (!Directory.Exists(ArtFolder))
            return null;

        string[] files = Directory.GetFiles(ArtFolder, "*.png");
        for (int i = 0; i < files.Length; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(files[i]);
            if (IsYellowName(fileName))
                return LoadSprite(ToAssetPath(files[i]));
        }

        return null;
    }

    static Sprite FindBaseSpinnerSprite(Sprite yellow)
    {
        if (!Directory.Exists(ArtFolder))
            return null;

        string[] files = Directory.GetFiles(ArtFolder, "*.png");
        for (int i = 0; i < files.Length; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(files[i]);
            if (IsYellowName(fileName))
                continue;
            if (fileName.IndexOf("bg", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            Sprite sprite = LoadSprite(ToAssetPath(files[i]));
            if (sprite != null && sprite != yellow)
                return sprite;
        }

        return null;
    }

    static bool IsYellowName(string fileName)
    {
        return fileName.IndexOf("sarı", System.StringComparison.OrdinalIgnoreCase) >= 0
               || fileName.IndexOf("sari", System.StringComparison.OrdinalIgnoreCase) >= 0
               || fileName.IndexOf("yellow", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static void ConfigureDroppedSprites()
    {
        if (!Directory.Exists(ArtFolder))
            return;

        string[] files = Directory.GetFiles(ArtFolder, "*.png");
        for (int i = 0; i < files.Length; i++)
            ConfigureSprite(ToAssetPath(files[i]));
    }

    static void ConfigureSprite(string assetPath)
    {
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

    static void EnsureFolders()
    {
        EnsureFolder("Assets/UI");
        EnsureFolder("Assets/UI/Loading");
        EnsureFolder(ArtFolder);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int lastSlash = path.LastIndexOf('/');
        string parent = path.Substring(0, lastSlash);
        string name = path.Substring(lastSlash + 1);
        AssetDatabase.CreateFolder(parent, name);
    }

    static Image FindImage(Transform root, string path)
    {
        Transform found = root.Find(path);
        return found != null ? found.GetComponent<Image>() : null;
    }

    static Sprite LoadSprite(string assetPath)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    static string ToAssetPath(string fullPath)
    {
        string normalized = fullPath.Replace('\\', '/');
        int assets = normalized.IndexOf("Assets/", System.StringComparison.Ordinal);
        return assets >= 0 ? normalized.Substring(assets) : normalized;
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

    static void SetCenter(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    static void SetBottom(RectTransform rect, float yFromBottom, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, yFromBottom);
        rect.sizeDelta = size;
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

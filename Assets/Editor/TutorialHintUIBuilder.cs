using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Authors Panel_Tutorial once, then never rebuilds it. MenuScene Hierarchy stays the source of
/// truth. Also slices panel_tutorial.png and refreshes the WASD TMP sprite asset.
/// </summary>
public static class TutorialHintUIBuilder
{
    const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
    const string GameScenePath = "Assets/Scenes/MainScene.unity";
    const string MenuCanvasName = "MainMenuCanvas";
    const string HudCanvasName = "InGameHudCanvas";
    const string KeysFolder = "Assets/UI/ingame/Tutorial/Keys";
    const string PanelSpritePath = KeysFolder + "/panel_tutorial.png";
    const string SpriteAssetPath = "Assets/TextMesh Pro/Resources/Sprite Assets/TutorialKeys.asset";

    [MenuItem("TCG Card Chaos/UI/Add Tutorial Hint")]
    public static void AddTutorialHint()
    {
        if (!File.Exists(MenuScenePath))
        {
            EditorUtility.DisplayDialog("Tutorial", "MenuScene not found.", "OK");
            return;
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ConfigurePanelSprite();
        ConfigureKeySprites();

        Scene menuScene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        Transform menuCanvas = FindInSceneTransform(menuScene, MenuCanvasName);
        if (menuCanvas == null)
        {
            EditorUtility.DisplayDialog("Tutorial", "MainMenuCanvas MenuScene'de yok.", "OK");
            return;
        }

        Transform menuPanel = menuCanvas.Find(TutorialHintView.PanelName);
        if (menuPanel == null)
            menuPanel = CreatePanel(menuCanvas).transform;

        ApplyPanelImage(menuPanel);
        WireLabel(menuPanel);
        menuPanel.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(menuScene);
        EditorSceneManager.SaveScene(menuScene, MenuScenePath);

        if (File.Exists(GameScenePath))
            CopyMenuPanelToHud(menuPanel);

        menuScene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        GameObject selected = FindInScene(menuScene, MenuCanvasName, TutorialHintView.PanelName);
        if (selected != null)
        {
            Selection.activeGameObject = selected;
            EditorGUIUtility.PingObject(selected);
        }

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(
                "Tutorial",
                "Kaynak: MenuScene / MainMenuCanvas / Panel_Tutorial\n"
                + "Düzenlemeyi MenuScene'de yap; New Game o hali çeker.\n"
                + "Panel varsa çocukları yeniden oluşturmuyorum.",
                "OK");
        }
    }

    static void CopyMenuPanelToHud(Transform menuPanel)
    {
        Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
        Transform hud = FindInSceneTransform(game, HudCanvasName);
        if (hud == null)
        {
            EditorSceneManager.CloseScene(game, true);
            Debug.LogError("[TutorialHintUIBuilder] InGameHudCanvas not found in MainScene.");
            return;
        }

        Transform old = hud.Find(TutorialHintView.PanelName);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        GameObject clone = Object.Instantiate(menuPanel.gameObject, hud, false);
        clone.name = TutorialHintView.PanelName;
        clone.SetActive(false);
        EditorSceneManager.MoveGameObjectToScene(clone, game);

        WireHudView(hud, clone.transform);
        EditorSceneManager.MarkSceneDirty(game);
        EditorSceneManager.SaveScene(game, GameScenePath);
        EditorSceneManager.CloseScene(game, true);
    }

    static void WireHudView(Transform hud, Transform panel)
    {
        TutorialHintView view = hud.GetComponent<TutorialHintView>();
        if (view == null)
            view = hud.gameObject.AddComponent<TutorialHintView>();

        var serialized = new SerializedObject(view);
        SerializedProperty root = serialized.FindProperty("root");
        if (root != null)
            root.objectReferenceValue = panel.gameObject;

        Transform label = panel.Find(TutorialHintView.LabelName);
        SerializedProperty labelProp = serialized.FindProperty("label");
        if (labelProp != null && label != null)
            labelProp.objectReferenceValue = label.GetComponent<TMP_Text>();

        serialized.ApplyModifiedPropertiesWithoutUndo();
        panel.SetAsLastSibling();
    }

    static GameObject CreatePanel(Transform canvas)
    {
        var go = new GameObject(TutorialHintView.PanelName, typeof(RectTransform));
        go.transform.SetParent(canvas, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(3.9f, -20f);
        rect.sizeDelta = new Vector2(640f, 72f);

        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        ApplyPanelImage(go.transform);

        var fitter = go.AddComponent<TutorialHintFitter>();

        var labelGo = new GameObject(TutorialHintView.LabelName, typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var labelRect = (RectTransform)labelGo.transform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.anchoredPosition = new Vector2(28f, -18f);
        labelRect.sizeDelta = new Vector2(580f, 36f);

        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 26f;
        tmp.enableAutoSizing = false;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.richText = true;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        UiMenuFont.Apply(tmp);
        if (tmp.font != null)
            tmp.fontSharedMaterial = tmp.font.material;

        TMP_SpriteAsset sprites = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
        if (sprites != null)
            tmp.spriteAsset = sprites;

        var localized = labelGo.AddComponent<LocalizedText>();
        var loc = new SerializedObject(localized);
        SerializedProperty key = loc.FindProperty("key");
        if (key != null)
            key.stringValue = LocalizationKeys.TutorialMove;
        SerializedProperty uppercase = loc.FindProperty("uppercase");
        if (uppercase != null)
            uppercase.boolValue = false;
        loc.ApplyModifiedPropertiesWithoutUndo();

        var fitterSo = new SerializedObject(fitter);
        SerializedProperty labelProp = fitterSo.FindProperty("label");
        if (labelProp != null)
            labelProp.objectReferenceValue = tmp;
        fitterSo.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }

    static void ApplyPanelImage(Transform panel)
    {
        var image = panel.GetComponent<Image>();
        if (image == null)
            return;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.fillCenter = true;
        image.preserveAspect = false;
        image.color = Color.white;
        image.raycastTarget = false;
        image.pixelsPerUnitMultiplier = 1f;
    }

    static void WireLabel(Transform panel)
    {
        Transform label = panel.Find(TutorialHintView.LabelName);
        if (label == null)
            return;

        TMP_Text tmp = label.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            tmp.richText = true;
            UiMenuFont.Apply(tmp);
            TMP_SpriteAsset sprites = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
            if (sprites != null)
                tmp.spriteAsset = sprites;
        }

        LocalizedText localized = label.GetComponent<LocalizedText>();
        if (localized == null)
            localized = label.gameObject.AddComponent<LocalizedText>();

        var loc = new SerializedObject(localized);
        SerializedProperty key = loc.FindProperty("key");
        if (key != null && string.IsNullOrEmpty(key.stringValue))
            key.stringValue = LocalizationKeys.TutorialMove;
        loc.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ConfigurePanelSprite()
    {
        var importer = AssetImporter.GetAtPath(PanelSpritePath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = new Vector4(22f, 22f, 10f, 22f);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    static void ConfigureKeySprites()
    {
        string[] names =
        {
            "w_icon.png",
            "a_icon.png",
            "s_icon.png",
            "d_icon.png",
            "e_icon.png",
            "q_icon.png",
            "mouse_left_click_icon.png",
            "mouse_wheel_up.png",
            "mouse_wheel_down.png",
            "shift_icon.png",
        };
        for (int i = 0; i < names.Length; i++)
        {
            string path = KeysFolder + "/" + names[i];
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
    }

    static Transform FindInSceneTransform(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == name)
                return roots[i].transform;

            Transform nested = FindRecursive(roots[i].transform, name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    static Transform FindRecursive(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child;

            Transform nested = FindRecursive(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    static GameObject FindInScene(Scene scene, string canvasName, string panelName)
    {
        Transform canvas = FindInSceneTransform(scene, canvasName);
        if (canvas == null)
            return null;

        Transform panel = canvas.Find(panelName);
        return panel != null ? panel.gameObject : null;
    }
}

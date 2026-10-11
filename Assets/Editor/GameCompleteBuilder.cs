using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class GameCompleteBuilder
{
    public const string Path = "Assets/Resources/UI/GameComplete.prefab";
    [MenuItem("TCG Card Chaos/UI/Rebuild Game Complete Popup")]
    public static void Build()
    {
        var root = Rect("GameComplete", null, Vector2.zero, Vector2.zero); Stretch(root);
        var panel = Rect("Panel", root, Vector2.zero, Vector2.zero); Stretch(panel);
        panel.gameObject.AddComponent<Image>().color = new Color(.015f, .025f, .08f, .86f);
        var body = Rect("Body", panel, new Vector2(0, 20), new Vector2(850, 480));
        var bg = body.gameObject.AddComponent<Image>();
        bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/CabinetTutorial/TutorialPanel.png");
        bg.type = Image.Type.Sliced;
        var title = Text(body, "Title", new Vector2(0, 148), new Vector2(750, 80), 48, LocalizationKeys.GameCompleteTitle);
        title.color = new Color32(255, 210, 0, 255);
        Text(body, "Message", new Vector2(0, -18), new Vector2(730, 230), 30, LocalizationKeys.GameCompleteMessage);
        var button = Rect("Continue", body, new Vector2(0, -240), new Vector2(280, 88));
        var image = button.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/CabinetTutorial/Continue.png");
        image.type = Image.Type.Sliced;
        var click = button.gameObject.AddComponent<Button>(); click.targetGraphic = image;
        SkillPanelButtonFeedback.Ensure(click);
        var label = Text(button, "Label", Vector2.zero, new Vector2(246, 66), 38, LocalizationKeys.MenuContinue);
        label.fontSharedMaterial = label.font.material;
        root.gameObject.AddComponent<GameCompleteView>();
        PrefabUtility.SaveAsPrefabAsset(root.gameObject, Path);
        Object.DestroyImmediate(root.gameObject);
    }

    static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = pos; rect.sizeDelta = size; return rect;
    }
    static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    static TMP_Text Text(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, string key)
    {
        var text = Rect(name, parent, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = UiMenuFont.Font; text.fontSharedMaterial = UiMenuFont.OutlineThin;
        text.color = Color.white; text.fontSize = fontSize;
        text.enableAutoSizing = true; text.fontSizeMin = 23; text.fontSizeMax = fontSize;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.gameObject.AddComponent<LocalizedText>().SetKey(key); return text;
    }
}

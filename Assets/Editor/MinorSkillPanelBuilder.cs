using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Only replaces the duplicate panel goal area and creates the separate unlock popup.</summary>
public static class MinorSkillPanelBuilder
{
    const string SkillPath = "Assets/Resources/UI/Skills/SkillUI.prefab";
    const string PopupPath = "Assets/Resources/UI/Skills/MinorSkillUnlock.prefab";

    [MenuItem("TCG Card Chaos/UI/Build Minor Skill Placeholders")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before updating minor skill UI.");
        var root = PrefabUtility.LoadPrefabContents(SkillPath);
        try
        {
            ApplyToRoot(root.transform);
            BuildPopup(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, SkillPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void ApplyToRoot(Transform root)
    {
        Transform body = root.Find("Panel/Body");
        AssignIcons(root.GetComponent<SkillPanelView>());
        // Keep the existing artwork/type sizes; translated descriptions need room for five lines.
        Transform details = body.Find("Details");
        Rect(details, 906, 232, 602, 590);
        Rect(details.Find("Description"), 32, 198, 538, 146);
        Rect(details.Find("StatsRule"), 30, 358, 542, 2);
        Rect(details.Find("Next"), 32, 376, 538, 42);
        Rect(details.Find("StatsFrame"), 26, 426, 550, 92);
        Rect(details.Find("Stats"), 42, 436, 518, 76);
        Rect(details.Find("Upgrade"), 28, 528, 546, 52);
        Rect(details.Find("Upgrade/Label"), 12, 0, 522, 52);
        ((RectTransform)body.Find("Divider")).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 590);
        Transform progress = body.Find("Progress");
        if (progress != null) UnityEngine.Object.DestroyImmediate(progress.gameObject);
        Transform old = body.Find("MinorSkills");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var group = NewRect(body, "MinorSkills");
        Rect(group, 100, 630, 730, 218);
        TMP_Text sourceText = body.Find("Nodes/Skill0/Name").GetComponent<TMP_Text>();
        var title = Label(group, "Title", sourceText, "Minor Abilities", 29 * .85f);
        title.alignment = TextAlignmentOptions.Left;
        Rect(title.transform, 22, 0, 686, 34);
        Image tile = body.Find("Nodes/Skill0").GetComponent<Image>();
        Image selection = body.Find("Nodes/Skill0/Selection").GetComponent<Image>();
        Sprite locked = root.Find("Hotbar/Skill0/LockIcon").GetComponent<Image>().sprite;
        for (int i = 0; i < MinorSkillProgress.Count; i++)
        {
            Image slot = Image(group, "Skill" + i, tile);
            Rect(slot.transform, 72 + i * 160, 46, 96, 100);
            var button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = slot;
            button.colors = tile.GetComponent<Button>().colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            slot.raycastTarget = true;
            Image icon = Image(slot.transform, "Icon", null);
            icon.sprite = locked;
            icon.preserveAspect = true;
            Rect(icon.transform, 4, 4, 88, 92);
            Image ring = Image(slot.transform, "Selection", selection);
            Rect(ring.transform, -5, -5, 106, 110);
            ring.gameObject.SetActive(false);
            TMP_Text name = Label(slot.transform, "Name", sourceText, "???", 20);
            Rect(name.transform, -28, 109, 152, 60);
            name.alignment = TextAlignmentOptions.Top;
        }
    }

    public static void UpdatePopupIconBackground()
    {
        var root = PrefabUtility.LoadPrefabContents(PopupPath);
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SkillPath);
            AddPopupIconBackground(root.transform.Find("Panel/Body"), source.transform);
            PrefabUtility.SaveAsPrefabAsset(root, PopupPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void AddPopupIconBackground(Transform body, Transform source)
    {
        var old = body.Find("IconBackground");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var background = Image(body, "IconBackground", source.Find("Panel/Body/Nodes/Skill0").GetComponent<Image>());
        Rect(background.transform, 336, 136, 128, 128);
        background.raycastTarget = false;
        var icon = body.Find("Icon");
        if (icon != null) background.transform.SetSiblingIndex(icon.GetSiblingIndex());
    }

    static void BuildPopup(Transform source)
    {
        GameObject root = new GameObject("MinorSkillUnlock", typeof(RectTransform), typeof(MinorSkillUnlockView));
        root.layer = 5;
        AssignIcons(root.GetComponent<MinorSkillUnlockView>());
        try
        {
            Stretch((RectTransform)root.transform);
            Image shade = Image(root.transform, "Panel", source.Find("Panel").GetComponent<Image>());
            Stretch((RectTransform)shade.transform);
            shade.raycastTarget = true;
            Image panel = Image(shade.transform, "Body", source.Find("Panel/Body").GetComponent<Image>());
            var body = (RectTransform)panel.transform;
            body.anchorMin = body.anchorMax = body.pivot = new Vector2(.5f, .5f);
            body.anchoredPosition = Vector2.zero;
            body.sizeDelta = new Vector2(800, 520);
            panel.raycastTarget = true;
            TMP_Text template = source.Find("Panel/Body/Details/Name").GetComponent<TMP_Text>();
            TMP_Text title = Label(body, "Title", template, "Ability unlocked", 38);
            Rect(title.transform, 64, 78, 672, 64);
            AddPopupIconBackground(body, source);
            Image icon = Image(body, "Icon", null);
            icon.sprite = source.Find("Panel/Body/Nodes/Skill0/Check").GetComponent<Image>().sprite;
            icon.preserveAspect = true;
            Rect(icon.transform, 348, 148, 104, 104);
            TMP_Text message = Label(body, "Message", template, "Unlocked ability: Minor Skill 1.", 28);
            Rect(message.transform, 76, 280, 648, 100);
            Image close = Image(body, "Close", source.Find("Panel/Body/Details/Upgrade").GetComponent<Image>());
            Rect(close.transform, 150, 388, 500, 64);
            var button = close.gameObject.AddComponent<Button>();
            button.targetGraphic = close;
            button.colors = source.Find("Panel/Body/Details/Upgrade").GetComponent<Button>().colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            close.raycastTarget = true;
            TMP_Text closeTemplate = source.Find("Panel/Body/Details/Upgrade/Label").GetComponent<TMP_Text>();
            TMP_Text label = Label(close.transform, "Label", closeTemplate, "Continue", 28);
            Rect(label.transform, 20, 5, 460, 54);
            shade.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PopupPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void AssignIcons(UnityEngine.Object target)
    {
        string[] names = { "LongJump", "Run", "CapacityPlus2", "CapacityPlus3" };
        var serialized = new SerializedObject(target);
        SerializedProperty icons = serialized.FindProperty("minorSkillIcons");
        icons.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            string path = "Assets/UI/Skills/Minor/" + names[i] + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Missing minor skill icon: " + path);
            icons.GetArrayElementAtIndex(i).objectReferenceValue = sprite;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image Image(Transform parent, string name, Image source)
    {
        var rect = NewRect(parent, name);
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        if (source != null)
        {
            image.sprite = source.sprite;
            image.color = source.color;
            image.type = source.type;
            image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            image.preserveAspect = source.preserveAspect;
            image.material = source.material;
        }
        return image;
    }

    static TMP_Text Label(Transform parent, string name, TMP_Text source, string text, float size)
    {
        var rect = NewRect(parent, name);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = source.font;
        label.fontSharedMaterial = source.fontSharedMaterial;
        label.color = source.color;
        label.text = text;
        label.fontSize = label.fontSizeMax = size;
        label.fontSizeMin = 17;
        label.enableAutoSizing = true;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static void Rect(Transform transform, float x, float y, float width, float height)
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared slot-row cloning for Load Game and Save Game lists.
/// Scene templates only ship a couple of authored rows; extras are copied at runtime.
/// </summary>
public static class SaveSlotListLayout
{
    const string ManualHeaderName = "ManualSavesHeader";
    const string AutoHeaderName = "AutosavesHeader";

    public static void HideLoadGroupHeaders(Transform root)
    {
        Transform content = FindContent(root);
        if (content == null)
            return;
        Transform manual = content.Find(ManualHeaderName);
        Transform auto = content.Find(AutoHeaderName);
        if (manual != null) manual.gameObject.SetActive(false);
        if (auto != null) auto.gameObject.SetActive(false);
    }

    public static void ShowLoadGroupHeaders(
        Transform root, LoadGameSlotView[] rows, int manualCount, int autoCount)
    {
        Transform content = FindContent(root);
        if (content == null)
            return;

        Transform manual = EnsureGroupHeader(content, ManualHeaderName, LocalizationKeys.LoadGameManualSaves);
        Transform auto = EnsureGroupHeader(content, AutoHeaderName, LocalizationKeys.LoadGameAutosaves);
        manual.gameObject.SetActive(manualCount > 0);
        auto.gameObject.SetActive(autoCount > 0);

        // Explicit ordering also handles refreshed lists after adding/deleting a save.
        manual.SetAsLastSibling();
        for (int i = 0; i < manualCount && i < rows.Length; i++)
            rows[i].transform.SetAsLastSibling();
        auto.SetAsLastSibling();
        for (int i = manualCount; i < rows.Length; i++)
            rows[i].transform.SetAsLastSibling();
    }

    static Transform EnsureGroupHeader(Transform content, string name, string key)
    {
        Transform existing = content.Find(name);
        if (existing != null)
            return existing;

        var header = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        header.transform.SetParent(content, false);
        LayoutElement layout = header.GetComponent<LayoutElement>();
        layout.minHeight = 44f;
        layout.preferredHeight = 44f;
        var text = header.AddComponent<TextMeshProUGUI>();
        text.fontSize = 28f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.margin = new Vector4(16f, 0f, 16f, 0f);
        text.color = Color.white;
        text.raycastTarget = false;
        UiMenuFont.Apply(text);
        header.AddComponent<LocalizedText>().SetKey(key);
        return header.transform;
    }

    public static Transform FindContent(Transform root)
    {
        if (root == null)
            return null;

        Transform content = root.Find("ListFrame/Viewport/Content");
        if (content != null)
            return content;

        ScrollRect scroll = root.GetComponentInChildren<ScrollRect>(true);
        return scroll != null ? scroll.content : null;
    }

    public static LoadGameSlotView[] CollectInOrder(Transform content)
    {
        if (content == null)
            return System.Array.Empty<LoadGameSlotView>();

        var ordered = new List<LoadGameSlotView>(content.childCount);
        for (int i = 0; i < content.childCount; i++)
        {
            LoadGameSlotView slot = content.GetChild(i).GetComponent<LoadGameSlotView>();
            if (slot != null)
                ordered.Add(slot);
        }

        return ordered.ToArray();
    }

    public static LoadGameSlotView[] EnsureRows(Transform root, int needed)
    {
        Transform content = FindContent(root);
        LoadGameSlotView[] existing = CollectInOrder(content);
        if (needed <= 0)
            return existing;

        LoadGameSlotView template = existing.Length > 0
            ? existing[0]
            : root != null
                ? root.GetComponentInChildren<LoadGameSlotView>(true)
                : null;
        if (template == null)
            return existing;

        if (content == null)
            content = template.transform.parent;
        if (content == null)
            return existing;

        for (int i = existing.Length; i < needed; i++)
        {
            GameObject extra = Object.Instantiate(template.gameObject, content, false);
            extra.name = "Slot_" + (i + 1);
            extra.SetActive(true);
        }

        return CollectInOrder(content);
    }
}

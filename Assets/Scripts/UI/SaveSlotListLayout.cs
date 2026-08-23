using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared slot-row cloning for Load Game and Save Game lists.
/// Scene templates only ship a couple of authored rows; extras are copied at runtime.
/// </summary>
public static class SaveSlotListLayout
{
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

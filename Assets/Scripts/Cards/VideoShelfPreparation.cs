using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>New-game set dressing for video_tcg_3, using existing floor cards only.</summary>
public static class VideoShelfPreparation
{
    public static IEnumerator FillHalfRoutine()
    {
        var byCategory = new Dictionary<string, List<WorldCard>>(StringComparer.Ordinal);
        WorldCard[] cards = UnityEngine.Object.FindObjectsByType<WorldCard>(FindObjectsSortMode.None);
        foreach (WorldCard card in cards)
        {
            if (card.Definition == null || card.UsesPsaSlab || card.IsInHand || card.IsFlyingToShelf
                || card.GetComponentInParent<CardShelfSlot>() != null
                || card.GetComponentInParent<PsaCabinetSlot>() != null)
                continue;
            string category = card.Definition.ShelfCategoryId;
            if (string.IsNullOrEmpty(category))
                continue;
            if (!byCategory.TryGetValue(category, out List<WorldCard> pool))
                byCategory.Add(category, pool = new List<WorldCard>());
            pool.Add(card);
        }
        foreach (List<WorldCard> pool in byCategory.Values)
            pool.Sort((a, b) => string.CompareOrdinal(a.Definition.DefinitionId, b.Definition.DefinitionId));

        CardShelf[] shelves = UnityEngine.Object.FindObjectsByType<CardShelf>(FindObjectsSortMode.None);
        // Stable scene order also keeps series assignments consistent between New Games.
        Array.Sort(shelves, (a, b) => string.CompareOrdinal(HierarchyPath(a.transform), HierarchyPath(b.transform)));
        int moved = 0;
        int completed = 0;
        foreach (CardShelf shelf in shelves)
        {
            shelf.RefreshSlotCache();
            CardShelfSlot[] slots = shelf.GetComponentsInChildren<CardShelfSlot>();
            Array.Sort(slots, (a, b) => a.RowIndex != b.RowIndex
                ? a.RowIndex.CompareTo(b.RowIndex) : a.SlotNumber.CompareTo(b.SlotNumber));
            int target = slots.Length / 2;
            int occupied = shelf.CountOccupiedSlots();
            byCategory.TryGetValue(shelf.CategoryId, out List<WorldCard> pool);
            foreach (CardShelfSlot slot in slots)
            {
                if (occupied >= target || pool == null)
                    break;
                if (!slot.IsEmpty)
                    continue;
                for (int i = 0; i < pool.Count; i++)
                {
                    WorldCard card = pool[i];
                    // Uses the same category, column number and series-row rules as gameplay.
                    if (!shelf.IsCorrectPlacement(card, slot))
                        continue;
                    if (!slot.RestoreOccupiedCard(card, shelf.SurfacePadding, true, playPlacementFeedback: false))
                        continue;
                    pool.RemoveAt(i);
                    occupied++;
                    moved++;
                    break;
                }
            }
            if (occupied >= target)
                completed++;
            else
                Debug.LogWarning($"[Video] {HierarchyPath(shelf.transform)}: {occupied}/{target} slots; insufficient matching floor cards.");
            yield return null;
        }
        Debug.Log($"[Video] Half-filled cabinets={completed}/{shelves.Length}, moved floor cards={moved}");
    }

    static string HierarchyPath(Transform item)
    {
        string path = item.name + "[" + item.GetSiblingIndex() + "]";
        while (item.parent != null)
        {
            item = item.parent;
            path = item.name + "[" + item.GetSiblingIndex() + "]/" + path;
        }
        return path;
    }
}

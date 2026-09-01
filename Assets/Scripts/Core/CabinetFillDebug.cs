using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Play-mode helper: take unique floor cards and seat them in the correct cabinet slots (O key).
/// </summary>
public static class CabinetFillDebug
{
    public const int DefaultCabinetCount = 30;

    public static int FillCabinets(int maxCabinetCount)
    {
        if (!GameScenes.IsActiveGameScene() || !CardInstancedRenderManager.IsGameplayReady)
            return 0;

        if (maxCabinetCount <= 0)
            return 0;

        CardCatalog.EnsureLoaded();
        CardArtLibrary.EnsureLoaded();

        CardShelf[] shelves = Object.FindObjectsByType<CardShelf>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        if (shelves.Length == 0)
            return 0;

        var ordered = new List<CardShelf>(shelves.Length);
        for (int i = 0; i < shelves.Length; i++)
        {
            if (shelves[i] != null)
                ordered.Add(shelves[i]);
        }

        ordered.Sort(CompareShelfOrder);

        var pool = new List<WorldCard>(2048);
        CollectGroundPool(pool);

        int filledCabinetCount = 0;
        for (int i = 0; i < ordered.Count && filledCabinetCount < maxCabinetCount; i++)
        {
            CardShelf shelf = ordered[i];
            if (shelf == null || shelf.IsComplete())
                continue;

            FillShelfFromGround(shelf, pool);
            filledCabinetCount++;
        }

        if (filledCabinetCount > 0)
        {
            GameSaveSignals.MarkDirty();
            GameProgressCounter.InvalidateCache();
        }

        return filledCabinetCount;
    }

    static void CollectGroundPool(List<WorldCard> pool)
    {
        pool.Clear();
        WorldCard[] cards = Object.FindObjectsByType<WorldCard>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < cards.Length; i++)
        {
            WorldCard card = cards[i];
            if (!IsGroundCandidate(card))
                continue;

            pool.Add(card);
        }
    }

    static bool IsGroundCandidate(WorldCard card)
    {
        if (card == null || card.Definition == null)
            return false;
        if (card.UsesPsaSlab || card.IsInHand || card.IsFlyingToShelf || card.IsShelfRowCompleteLocked)
            return false;
        if (card.GetComponentInParent<CardShelfSlot>() != null)
            return false;
        if (card.GetComponentInParent<PsaCabinetSlot>() != null)
            return false;

        return true;
    }

    static void FillShelfFromGround(CardShelf shelf, List<WorldCard> pool)
    {
        shelf.RefreshSlotCache();

        CardShelfSlot[] slots = shelf.GetComponentsInChildren<CardShelfSlot>(true);
        if (slots.Length == 0)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].SyncIndicesFromHierarchy();
        }

        System.Array.Sort(slots, CompareSlotOrder);

        var slotsByRow = new Dictionary<int, List<CardShelfSlot>>(16);
        var claimedSeries = new HashSet<string>();
        var seriesByRow = new Dictionary<int, string>(16);

        for (int i = 0; i < slots.Length; i++)
        {
            CardShelfSlot slot = slots[i];
            if (slot == null || !slot.gameObject.activeInHierarchy)
                continue;

            slot.RefreshOccupancy();
            if (!slotsByRow.TryGetValue(slot.RowIndex, out List<CardShelfSlot> rowSlots))
            {
                rowSlots = new List<CardShelfSlot>(10);
                slotsByRow.Add(slot.RowIndex, rowSlots);
            }

            rowSlots.Add(slot);

            if (slot.IsEmpty)
                continue;

            WorldCard seated = slot.OccupiedCard;
            if (seated == null || !shelf.IsCorrectPlacement(seated, slot))
                continue;

            if (!CardShelfSeries.TryGetSeriesId(seated.Definition, out string seriesId))
                continue;

            if (!seriesByRow.ContainsKey(slot.RowIndex))
                seriesByRow[slot.RowIndex] = seriesId;

            claimedSeries.Add(seriesId);
        }

        var rowKeys = new List<int>(slotsByRow.Keys);
        rowKeys.Sort();

        string categoryId = shelf.CategoryId;
        float padding = shelf.SurfacePadding;

        for (int r = 0; r < rowKeys.Count; r++)
        {
            int row = rowKeys[r];
            List<CardShelfSlot> rowSlots = slotsByRow[row];
            if (!seriesByRow.TryGetValue(row, out string seriesId))
            {
                seriesId = PickUnusedSeries(pool, categoryId, claimedSeries);
                if (string.IsNullOrEmpty(seriesId))
                    continue;

                seriesByRow[row] = seriesId;
                claimedSeries.Add(seriesId);
            }

            for (int i = 0; i < rowSlots.Count; i++)
            {
                CardShelfSlot slot = rowSlots[i];
                if (slot == null)
                    continue;

                slot.RefreshOccupancy();
                if (!slot.IsEmpty)
                    continue;

                int slotNumber = shelf.ResolveSlotNumber(slot);
                if (!CardShelfCategories.IsValidSlotNumber(slotNumber, shelf.SlotsPerRow))
                    continue;

                WorldCard card = TakeGroundCard(pool, categoryId, seriesId, slotNumber);
                if (card == null)
                    continue;

                slot.RestoreOccupiedCard(card, padding, isCorrect: true, playPlacementFeedback: false);
            }
        }

        CabinetSignCompleteOverlay.Refresh(shelf);
    }

    static string PickUnusedSeries(List<WorldCard> pool, string categoryId, HashSet<string> claimedSeries)
    {
        string best = null;
        for (int i = 0; i < pool.Count; i++)
        {
            WorldCard card = pool[i];
            if (!IsGroundCandidate(card))
                continue;
            if (!CardShelfRules.CategoriesMatch(categoryId, card.ShelfCategoryId))
                continue;
            if (!CardShelfSeries.TryGetSeriesId(card.Definition, out string seriesId))
                continue;
            if (claimedSeries.Contains(seriesId))
                continue;
            if (best == null || string.CompareOrdinal(seriesId, best) < 0)
                best = seriesId;
        }

        return best;
    }

    static WorldCard TakeGroundCard(
        List<WorldCard> pool,
        string categoryId,
        string seriesId,
        int slotNumber)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            WorldCard card = pool[i];
            if (!IsGroundCandidate(card))
                continue;
            if (!CardShelfRules.CategoriesMatch(categoryId, card.ShelfCategoryId))
                continue;
            if (card.ShelfSlotNumber != slotNumber)
                continue;
            if (!CardShelfSeries.TryGetSeriesId(card.Definition, out string cardSeries))
                continue;
            if (!string.Equals(cardSeries, seriesId, System.StringComparison.Ordinal))
                continue;

            pool.RemoveAt(i);
            return card;
        }

        return null;
    }

    static int CompareShelfOrder(CardShelf a, CardShelf b)
    {
        if (a == b)
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        Vector3 pa = a.transform.position;
        Vector3 pb = b.transform.position;
        int compare = pa.z.CompareTo(pb.z);
        if (compare != 0)
            return compare;

        compare = pa.x.CompareTo(pb.x);
        if (compare != 0)
            return compare;

        compare = pa.y.CompareTo(pb.y);
        if (compare != 0)
            return compare;

        return a.GetInstanceID().CompareTo(b.GetInstanceID());
    }

    static int CompareSlotOrder(CardShelfSlot a, CardShelfSlot b)
    {
        if (a == b)
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        int row = a.RowIndex.CompareTo(b.RowIndex);
        if (row != 0)
            return row;

        CardShelf shelf = a.GetComponentInParent<CardShelf>();
        int slotA = shelf != null ? shelf.ResolveSlotNumber(a) : a.ColumnIndex;
        int slotB = shelf != null ? shelf.ResolveSlotNumber(b) : b.ColumnIndex;
        return slotA.CompareTo(slotB);
    }
}

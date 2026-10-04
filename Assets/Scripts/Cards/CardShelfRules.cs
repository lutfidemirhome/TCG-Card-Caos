using System.Collections.Generic;

/// <summary>
/// Shared rules for matching world cards to cabinet slots.
/// </summary>
public static class CardShelfRules
{
    struct RowClaimCandidate : System.IComparable<RowClaimCandidate>
    {
        public CardShelfSlot Slot;
        public string SeriesId;
        public long Order;
        public int SlotIndex;

        public int CompareTo(RowClaimCandidate other)
        {
            int order = Order.CompareTo(other.Order);
            return order != 0 ? order : SlotIndex.CompareTo(other.SlotIndex);
        }
    }

    // Validation runs synchronously on Unity's main thread and never invokes callbacks.
    // Reuse scratch storage because progress and placement repeatedly inspect each cabinet.
    static readonly List<RowClaimCandidate> ClaimCandidates = new List<RowClaimCandidate>(100);
    static readonly Dictionary<int, string> SeriesByRow = new Dictionary<int, string>(10);
    static readonly Dictionary<string, int> RowBySeries =
        new Dictionary<string, int>(System.StringComparer.Ordinal);

    public static bool CategoriesMatch(string shelfCategoryId, string cardCategoryId)
    {
        if (string.IsNullOrWhiteSpace(shelfCategoryId) || string.IsNullOrWhiteSpace(cardCategoryId))
            return false;

        return string.Equals(shelfCategoryId, cardCategoryId, System.StringComparison.Ordinal);
    }

    public static bool SlotMatches(int slotNumber, int requiredSlotNumber, int slotsPerRow)
    {
        if (!CardShelfCategories.IsValidSlotNumber(requiredSlotNumber, slotsPerRow))
            return false;

        return slotNumber == CardCatalog.NormalizeSlotNumber(requiredSlotNumber);
    }

    public static bool SlotMatches(int slotNumber, int requiredSlotNumber)
    {
        return SlotMatches(slotNumber, requiredSlotNumber, CardShelfCategories.DefaultSlotsPerRow);
    }

    public static bool CanPlaceOnShelf(string shelfCategoryId, CardDefinition definition)
    {
        if (definition == null)
            return false;

        return CategoriesMatch(shelfCategoryId, definition.ShelfCategoryId);
    }

    public static bool CanPlaceInSlot(string shelfCategoryId, CardDefinition definition, CardShelfSlot slot)
    {
        if (definition == null || slot == null)
            return false;

        if (!CanPlaceOnShelf(shelfCategoryId, definition))
            return false;

        return SlotMatches(slot.SlotNumber, definition.ShelfSlotNumber, slot.OwnerShelfSlotsPerRow);
    }

    public static bool IsCorrectShelfPlacement(
        string shelfCategoryId,
        CardDefinition definition,
        CardShelfSlot slot,
        IReadOnlyList<CardShelfSlot> occupiedSlots = null,
        CardShelf ownerShelf = null)
    {
        if (definition == null || slot == null)
            return false;

        if (!CanPlaceInSlot(shelfCategoryId, definition, slot))
            return false;

        return MatchesSeriesRow(definition, slot, occupiedSlots, null, shelfCategoryId, ownerShelf);
    }

    /// <summary>
    /// The first correctly numbered card claims its row for its series. Later foreign
    /// cards do not invalidate that owner or claim another row for their own series.
    /// Removing cards rebuilds ownership from the earliest remaining eligible placement.
    /// </summary>
    public static bool MatchesSeriesRow(
        CardDefinition definition,
        CardShelfSlot slot,
        IReadOnlyList<CardShelfSlot> occupiedSlots,
        CardShelfSlot excludeSlot,
        string shelfCategoryId = null,
        CardShelf ownerShelf = null)
    {
        if (definition == null || slot == null)
            return false;

        if (!CardShelfSeries.TryGetSeriesId(definition, out string seriesId))
            return true;

        RebuildRowClaims(occupiedSlots, excludeSlot, shelfCategoryId, ownerShelf);
        if (RowBySeries.TryGetValue(seriesId, out int assignedRow) && slot.RowIndex != assignedRow)
            return false;

        return !SeriesByRow.TryGetValue(slot.RowIndex, out string rowSeries)
            || string.Equals(rowSeries, seriesId, System.StringComparison.Ordinal);
    }

    static void RebuildRowClaims(
        IReadOnlyList<CardShelfSlot> occupiedSlots,
        CardShelfSlot excludeSlot,
        string shelfCategoryId,
        CardShelf ownerShelf)
    {
        ClaimCandidates.Clear();
        SeriesByRow.Clear();
        RowBySeries.Clear();
        if (occupiedSlots == null)
            return;

        // The caller already owns this slot list. Reuse its resolved numbering instead
        // of searching the transform ancestry twice per card on every glow update.
        int slotsPerRow = ownerShelf != null ? ownerShelf.SlotsPerRow : 0;

        for (int i = 0; i < occupiedSlots.Count; i++)
        {
            CardShelfSlot occupiedSlot = occupiedSlots[i];
            if (occupiedSlot == null || occupiedSlot == excludeSlot || occupiedSlot.IsEmpty)
                continue;

            WorldCard card = occupiedSlot.OccupiedCard;
            if (card == null || card.Definition == null
                || (!string.IsNullOrWhiteSpace(shelfCategoryId)
                    && !CategoriesMatch(shelfCategoryId, card.Definition.ShelfCategoryId))
                || !SlotMatches(ownerShelf != null ? ownerShelf.ResolveSlotNumber(occupiedSlot) : occupiedSlot.SlotNumber,
                    card.Definition.ShelfSlotNumber,
                    ownerShelf != null ? slotsPerRow : occupiedSlot.OwnerShelfSlotsPerRow)
                || !CardShelfSeries.TryGetSeriesId(card.Definition, out string occupiedSeriesId))
                continue;

            ClaimCandidates.Add(new RowClaimCandidate
            {
                Slot = occupiedSlot,
                SeriesId = occupiedSeriesId,
                Order = occupiedSlot.PlacementOrder,
                SlotIndex = i,
            });
        }

        ClaimCandidates.Sort();
        for (int i = 0; i < ClaimCandidates.Count; i++)
        {
            RowClaimCandidate candidate = ClaimCandidates[i];
            int row = candidate.Slot.RowIndex;
            // A card on another series' row is invalid, so it must not claim its
            // own series elsewhere merely because it is present in the slot list.
            if (SeriesByRow.ContainsKey(row) || RowBySeries.ContainsKey(candidate.SeriesId))
                continue;
            SeriesByRow.Add(row, candidate.SeriesId);
            RowBySeries.Add(candidate.SeriesId, row);
        }
        ClaimCandidates.Clear();
    }
}

using System.Collections.Generic;

/// <summary>
/// Shared rules for matching world cards to cabinet slots.
/// </summary>
public static class CardShelfRules
{
    public static bool CategoriesMatch(string shelfCategoryId, string cardCategoryId)
    {
        if (string.IsNullOrWhiteSpace(shelfCategoryId) || string.IsNullOrWhiteSpace(cardCategoryId))
            return false;

        return string.Equals(shelfCategoryId, cardCategoryId, System.StringComparison.Ordinal);
    }

    public static bool SlotMatches(int slotNumber, int requiredSlotNumber)
    {
        return slotNumber == CardCatalog.NormalizeSlotNumber(requiredSlotNumber);
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

        return SlotMatches(slot.SlotNumber, definition.ShelfSlotNumber);
    }

    public static bool IsCorrectShelfPlacement(
        string shelfCategoryId,
        CardDefinition definition,
        CardShelfSlot slot,
        IReadOnlyList<CardShelfSlot> occupiedSlots = null)
    {
        if (definition == null || slot == null)
            return false;

        if (!CanPlaceInSlot(shelfCategoryId, definition, slot))
            return false;

        return MatchesSeriesRow(definition, slot, occupiedSlots, null);
    }

    /// <summary>
    /// First card of a series claims a row; later cards from that series must use the same row.
    /// A row cannot mix cards from different series.
    /// </summary>
    public static bool MatchesSeriesRow(
        CardDefinition definition,
        CardShelfSlot slot,
        IReadOnlyList<CardShelfSlot> occupiedSlots,
        CardShelfSlot excludeSlot)
    {
        if (definition == null || slot == null)
            return false;

        if (!CardShelfSeries.TryGetSeriesId(definition, out string seriesId))
            return true;

        int? assignedRow = FindRowForSeries(seriesId, occupiedSlots, excludeSlot);
        if (assignedRow.HasValue)
            return slot.RowIndex == assignedRow.Value;

        string rowSeries = FindSeriesOnRow(slot.RowIndex, occupiedSlots, excludeSlot);
        return rowSeries == null || rowSeries == seriesId;
    }

    static int? FindRowForSeries(
        string seriesId,
        IReadOnlyList<CardShelfSlot> occupiedSlots,
        CardShelfSlot excludeSlot)
    {
        if (occupiedSlots == null || string.IsNullOrWhiteSpace(seriesId))
            return null;

        int? foundRow = null;
        for (int i = 0; i < occupiedSlots.Count; i++)
        {
            CardShelfSlot occupiedSlot = occupiedSlots[i];
            if (occupiedSlot == null || occupiedSlot == excludeSlot || occupiedSlot.IsEmpty)
                continue;

            WorldCard card = occupiedSlot.OccupiedCard;
            if (card == null || card.Definition == null)
                continue;

            if (!CardShelfSeries.TryGetSeriesId(card.Definition, out string occupiedSeriesId))
                continue;

            if (!string.Equals(occupiedSeriesId, seriesId, System.StringComparison.Ordinal))
                continue;

            if (!foundRow.HasValue)
                foundRow = occupiedSlot.RowIndex;
            else if (foundRow.Value != occupiedSlot.RowIndex)
                return foundRow;
        }

        return foundRow;
    }

    static string FindSeriesOnRow(
        int rowIndex,
        IReadOnlyList<CardShelfSlot> occupiedSlots,
        CardShelfSlot excludeSlot)
    {
        if (occupiedSlots == null)
            return null;

        for (int i = 0; i < occupiedSlots.Count; i++)
        {
            CardShelfSlot occupiedSlot = occupiedSlots[i];
            if (occupiedSlot == null
                || occupiedSlot == excludeSlot
                || occupiedSlot.IsEmpty
                || occupiedSlot.RowIndex != rowIndex)
                continue;

            WorldCard card = occupiedSlot.OccupiedCard;
            if (card == null || card.Definition == null)
                continue;

            if (CardShelfSeries.TryGetSeriesId(card.Definition, out string occupiedSeriesId))
                return occupiedSeriesId;
        }

        return null;
    }
}

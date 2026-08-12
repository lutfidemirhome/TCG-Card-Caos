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

    public static bool IsCorrectShelfPlacement(string shelfCategoryId, CardDefinition definition, CardShelfSlot slot)
    {
        if (definition == null || slot == null)
            return false;

        return CanPlaceInSlot(shelfCategoryId, definition, slot);
    }
}

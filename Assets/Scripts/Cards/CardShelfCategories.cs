/// <summary>
/// Cabinet category ids. Each shelf prefab uses a <see cref="CardShelfCategoryDefinition"/> for symbols + id.
/// </summary>
public static class CardShelfCategories
{
    public const string NormalCommon = "normal_common";
    public const string NormalUncommon = "normal_uncommon";
    public const string NormalRare = "normal_rare";

    public const string FireCommon = "fire_common";
    public const string FireUncommon = "fire_uncommon";
    public const string FireRare = "fire_rare";

    public const int MinSlotNumber = 1;
    public const int MaxSlotNumber = 10;
    public const int DefaultSlotsPerRow = 10;
    public const int SlotsPerRow = DefaultSlotsPerRow;

    public static string GetDisplayName(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            return string.Empty;

        if (categoryId == NormalCommon)
            return "Normal Common";

        if (categoryId == NormalUncommon)
            return "Normal Uncommon";

        if (categoryId == NormalRare)
            return "Normal Rare";

        if (categoryId == FireCommon)
            return "Fire Common";

        if (categoryId == FireUncommon)
            return "Fire Uncommon";

        if (categoryId == FireRare)
            return "Fire Rare";

        return categoryId.Replace('_', ' ');
    }

    public static int GetDefaultSlotsPerRow(string categoryId)
    {
        if (categoryId == NormalUncommon || categoryId == FireUncommon)
            return 5;

        if (categoryId == NormalRare || categoryId == FireRare)
            return 3;

        return DefaultSlotsPerRow;
    }

    public static bool IsValidSlotNumber(int slotNumber, int slotsPerRow)
    {
        slotsPerRow = UnityEngine.Mathf.Clamp(slotsPerRow, MinSlotNumber, MaxSlotNumber);
        return slotNumber >= MinSlotNumber && slotNumber <= slotsPerRow;
    }

    public static bool IsValidSlotNumber(int slotNumber)
    {
        return IsValidSlotNumber(slotNumber, MaxSlotNumber);
    }

    /// <summary>
    /// Maps authored column index (0 at shelf local -X) to customer-facing slot number (1 = left).
    /// </summary>
    public static int ColumnToSlotNumber(int columnIndex, int slotsPerRow)
    {
        slotsPerRow = UnityEngine.Mathf.Clamp(slotsPerRow, MinSlotNumber, MaxSlotNumber);
        int clampedColumn = UnityEngine.Mathf.Clamp(columnIndex, 0, slotsPerRow - 1);
        return slotsPerRow - clampedColumn;
    }

    public static int ColumnToSlotNumber(int columnIndex)
    {
        return ColumnToSlotNumber(columnIndex, DefaultSlotsPerRow);
    }
}

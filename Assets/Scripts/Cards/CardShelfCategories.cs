/// <summary>
/// Cabinet category ids. Each shelf prefab uses a <see cref="CardShelfCategoryDefinition"/> for symbols + id.
/// </summary>
public static class CardShelfCategories
{
    public const string NormalCommon = "normal_common";
    public const string NormalUncommon = "normal_uncommon";

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

        return categoryId.Replace('_', ' ');
    }

    public static int GetDefaultSlotsPerRow(string categoryId)
    {
        if (categoryId == NormalUncommon)
            return 5;

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

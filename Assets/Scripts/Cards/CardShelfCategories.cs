/// <summary>
/// Cabinet category ids. Each shelf prefab uses a <see cref="CardShelfCategoryDefinition"/> for symbols + id.
/// </summary>
public static class CardShelfCategories
{
    public const string NormalCommon = "normal_common";

    public const int MinSlotNumber = 1;
    public const int MaxSlotNumber = 10;
    public const int SlotsPerRow = 10;

    public static string GetDisplayName(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            return string.Empty;

        if (categoryId == NormalCommon)
            return "Normal Common";

        return categoryId.Replace('_', ' ');
    }

    public static bool IsValidSlotNumber(int slotNumber)
    {
        return slotNumber >= MinSlotNumber && slotNumber <= MaxSlotNumber;
    }

    /// <summary>
    /// Maps authored column index (0 at shelf local -X) to customer-facing slot 1–10 (1 = left).
    /// </summary>
    public static int ColumnToSlotNumber(int columnIndex)
    {
        int clampedColumn = UnityEngine.Mathf.Clamp(columnIndex, 0, SlotsPerRow - 1);
        return MaxSlotNumber - clampedColumn;
    }
}

/// <summary>
/// Cabinet category ids. Labels use display names for now; UI symbols will replace text later.
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
}

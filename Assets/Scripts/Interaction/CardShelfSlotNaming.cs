/// <summary>
/// Parses slot object names created by the shelf authoring tools.
/// Example: CardShelfSlot_3_7 → row 3, column 7, slot number 3 (customer left = 1).
/// </summary>
public static class CardShelfSlotNaming
{
    public const string Prefix = "CardShelfSlot_";

    public static string BuildName(int rowIndex, int columnIndex)
    {
        return Prefix + rowIndex + "_" + columnIndex;
    }

    public static bool TryParse(string objectName, out int rowIndex, out int columnIndex)
    {
        rowIndex = 0;
        columnIndex = 0;

        if (string.IsNullOrWhiteSpace(objectName) || !objectName.StartsWith(Prefix))
            return false;

        string suffix = objectName.Substring(Prefix.Length);
        int separator = suffix.IndexOf('_');
        if (separator <= 0 || separator >= suffix.Length - 1)
            return false;

        if (!int.TryParse(suffix.Substring(0, separator), out rowIndex))
            return false;

        if (!int.TryParse(suffix.Substring(separator + 1), out columnIndex))
            return false;

        rowIndex = UnityEngine.Mathf.Max(0, rowIndex);
        columnIndex = UnityEngine.Mathf.Clamp(columnIndex, 0, CardShelfCategories.MaxSlotNumber - 1);
        return true;
    }

    /// <summary>ShelfSlots_Level → 0, ShelfSlots_Level (3) → 3.</summary>
    public static bool TryParseLevelRowIndex(string levelObjectName, out int rowIndex)
    {
        rowIndex = 0;
        const string prefix = "ShelfSlots_Level";
        if (string.IsNullOrWhiteSpace(levelObjectName) || !levelObjectName.StartsWith(prefix))
            return false;

        if (levelObjectName.Length == prefix.Length)
            return true;

        int open = levelObjectName.LastIndexOf('(');
        int close = levelObjectName.LastIndexOf(')');
        if (open < 0 || close <= open)
            return false;

        if (!int.TryParse(levelObjectName.Substring(open + 1, close - open - 1), out rowIndex))
            return false;

        rowIndex = UnityEngine.Mathf.Max(0, rowIndex);
        return true;
    }
}

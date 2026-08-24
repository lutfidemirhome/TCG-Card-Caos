using System.Globalization;

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

    public const string GrassCommon = "grass_common";
    public const string GrassUncommon = "grass_uncommon";
    public const string GrassRare = "grass_rare";
    public const string GrassEpic = "grass_epic";

    public const string WaterCommon = "water_common";
    public const string WaterUncommon = "water_uncommon";
    public const string WaterRare = "water_rare";

    public const string FlyingCommon = "flying_common";
    public const string FlyingUncommon = "flying_uncommon";
    public const string FlyingRare = "flying_rare";

    public const string GhostCommon = "ghost_common";
    public const string GhostUncommon = "ghost_uncommon";
    public const string GhostRare = "ghost_rare";

    public const string IceCommon = "ice_common";
    public const string IceUncommon = "ice_uncommon";
    public const string IceRare = "ice_rare";

    public const string SteelCommon = "steel_common";
    public const string SteelUncommon = "steel_uncommon";
    public const string SteelRare = "steel_rare";

    public const string DarknessCommon = "darkness_common";
    public const string DarknessUncommon = "darkness_uncommon";
    public const string DarknessRare = "darkness_rare";
    public const string DarknessEpic = "darkness_epic";

    public const string GroundCommon = "ground_common";
    public const string GroundUncommon = "ground_uncommon";
    public const string GroundRare = "ground_rare";

    public const string RockCommon = "rock_common";
    public const string RockUncommon = "rock_uncommon";
    public const string RockRare = "rock_rare";

    public const string PoisonCommon = "poison_common";
    public const string PoisonUncommon = "poison_uncommon";
    public const string PoisonRare = "poison_rare";

    public const string FairyCommon = "fairy_common";
    public const string FairyUncommon = "fairy_uncommon";
    public const string FairyRare = "fairy_rare";

    public const string FightingCommon = "fighting_common";
    public const string FightingUncommon = "fighting_uncommon";
    public const string FightingRare = "fighting_rare";
    public const string FightingEpic = "fighting_epic";

    public const string PsychicCommon = "psychic_common";
    public const string PsychicUncommon = "psychic_uncommon";
    public const string PsychicRare = "psychic_rare";

    public const string DragonCommon = "dragon_common";
    public const string DragonUncommon = "dragon_uncommon";
    public const string DragonRare = "dragon_rare";
    public const string DragonEpic = "dragon_epic";

    public const string LightningCommon = "lightning_common";
    public const string LightningUncommon = "lightning_uncommon";
    public const string LightningRare = "lightning_rare";

    public const string BugCommon = "bug_common";
    public const string BugUncommon = "bug_uncommon";
    public const string BugRare = "bug_rare";
    public const string BugEpic = "bug_epic";

    public const string TrainerAlly = "trainer_ally";
    public const string TrainerEquip = "trainer_equip";
    public const string TrainerGear = "trainer_gear";
    public const string TrainerZone = "trainer_zone";

    public const int MinSlotNumber = 1;
    public const int MaxSlotNumber = 10;
    public const int DefaultSlotsPerRow = 10;
    public const int SlotsPerRow = DefaultSlotsPerRow;

    public static string GetDisplayName(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            return string.Empty;

        string[] parts = categoryId.Split('_');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
                continue;

            parts[i] = char.ToUpper(parts[i][0], CultureInfo.InvariantCulture) +
                       parts[i].Substring(1);
        }

        return string.Join(" ", parts);
    }

    public static int GetDefaultSlotsPerRow(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            return DefaultSlotsPerRow;

        if (categoryId.EndsWith("_uncommon") || categoryId.EndsWith("_epic"))
            return 5;

        if (categoryId.EndsWith("_rare"))
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

using System;

[Serializable]
public class GameSaveData
{
    public int saveVersion = GameSaveSettings.CurrentSaveVersion;
    public string slotId = string.Empty;
    public SaveSlotType slotType;
    public int slotIndex;
    public string buildVariant = GameBuildVariant.Full;
    public string worldId = GameScenes.Game;
    public long timestampUnix;
    public double playTimeSeconds;
    public int cardsPlaced;
    public int totalCards;
    public int shelvesCompleted;
    public int totalShelves;
    public int cabinetsCompleted;
    public int totalCabinets;
    public int handSelectedIndex;
    public CardSaveRecord[] cards = Array.Empty<CardSaveRecord>();
    public PackSaveRecord[] packs = Array.Empty<PackSaveRecord>();

    public SaveSlotMetadata ToMetadata(bool thumbnailAvailable)
    {
        return new SaveSlotMetadata
        {
            saveVersion = saveVersion,
            slotId = slotId,
            slotType = slotType,
            slotIndex = slotIndex,
            buildVariant = buildVariant,
            worldId = worldId,
            timestampUnix = timestampUnix,
            playTimeSeconds = playTimeSeconds,
            cardsPlaced = cardsPlaced,
            totalCards = totalCards,
            shelvesCompleted = shelvesCompleted,
            totalShelves = totalShelves,
            cabinetsCompleted = cabinetsCompleted,
            totalCabinets = totalCabinets,
            thumbnailAvailable = thumbnailAvailable,
            isValid = true,
        };
    }
}

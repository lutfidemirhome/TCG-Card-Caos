using System;

[Serializable]
public class SaveSlotMetadata
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
    public bool thumbnailAvailable;
    public bool isValid = true;

    public DateTimeOffset TimestampUtc =>
        DateTimeOffset.FromUnixTimeSeconds(Math.Max(0, timestampUnix));
}

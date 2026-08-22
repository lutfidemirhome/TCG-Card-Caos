using System.Collections.Generic;

/// <summary>
/// Public save API for menus and gameplay. UI must not read files or JSON.
/// </summary>
public static class GameSaveStore
{
    public static bool HasAnySave() => GameSaveManager.HasAnyCompatibleSave();

    public static List<SaveSlotMetadata> GetSaveSlots() => GameSaveManager.GetSaveSlots();

    public static SaveSlotMetadata GetLatestValidSave() => GameSaveManager.GetLatestValidSave();

    public static void LoadSaveSlot(string slotId) => GameSaveManager.LoadSaveSlot(slotId);

    public static void SaveManual(string slotId = null)
    {
        GameSaveManager.EnsureExists().SaveManual(slotId);
    }

    public static void DeleteSaveSlot(string slotId) => GameSaveManager.DeleteSaveSlot(slotId);

    public static void SaveAndQuit()
    {
        GameSaveManager.EnsureExists().SaveAndQuit();
    }

    public static void SaveBeforeLeaveGameplay()
    {
        GameSaveManager.EnsureExists().SaveBeforeLeaveGameplay();
    }

    public static void CreateNewGame() => GameSaveManager.CreateNewGame();

    public static bool TryApplyLatestSave(out string error) => GameSaveManager.TryRestorePending(out error);
}

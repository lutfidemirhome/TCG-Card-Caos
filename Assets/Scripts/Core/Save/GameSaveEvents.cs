using System;

public static class GameSaveEvents
{
    public static event Action<string> SaveStarted;
    public static event Action<SaveSlotMetadata> SaveCompleted;
    public static event Action<string> SaveFailed;
    public static event Action<string> LoadStarted;
    public static event Action<string> LoadCompleted;
    public static event Action<string> LoadFailed;

    public static void RaiseSaveStarted(string slotId) => SaveStarted?.Invoke(slotId);
    public static void RaiseSaveCompleted(SaveSlotMetadata metadata) => SaveCompleted?.Invoke(metadata);
    public static void RaiseSaveFailed(string message) => SaveFailed?.Invoke(message);
    public static void RaiseLoadStarted(string slotId) => LoadStarted?.Invoke(slotId);
    public static void RaiseLoadCompleted(string slotId) => LoadCompleted?.Invoke(slotId);
    public static void RaiseLoadFailed(string message) => LoadFailed?.Invoke(message);
}

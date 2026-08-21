using System.IO;
using UnityEngine;

/// <summary>
/// Tracks whether a latest save exists and loads it when the player chooses Continue.
/// Gameplay systems can write to <see cref="LatestSavePath"/> when persistence is implemented.
/// </summary>
public static class GameSaveStore
{
    public const string LatestSaveFileName = "save_latest.dat";

    public static string LatestSavePath =>
        Path.Combine(Application.persistentDataPath, LatestSaveFileName);

    public static bool HasAnySave() => File.Exists(LatestSavePath);

    public static bool TryApplyLatestSave(out string error)
    {
        if (!HasAnySave())
        {
            error = "No save file found.";
            return false;
        }

        // Persistence hooks will deserialize and apply state here.
        Debug.Log("[GameSaveStore] Continue requested from " + LatestSavePath + " (load not implemented yet).");
        error = null;
        return true;
    }
}

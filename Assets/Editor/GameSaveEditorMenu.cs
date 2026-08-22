using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameSaveEditorMenu
{
    [MenuItem("TCG Card Caos/Save/Autosave Now")]
    public static void AutosaveNow()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Save", "Enter Play Mode first.", "OK");
            return;
        }

        GameSaveManager.EnsureExists().ForceAutosaveNow();
    }

    [MenuItem("TCG Card Caos/Save/Load Latest")]
    public static void LoadLatest()
    {
        SaveSlotMetadata latest = GameSaveManager.GetLatestValidSave();
        if (latest == null)
        {
            EditorUtility.DisplayDialog("Save", "No compatible save found.", "OK");
            return;
        }

        if (Application.isPlaying)
        {
            GameSaveStore.LoadSaveSlot(latest.slotId);
            return;
        }

        UnityEditor.SessionState.SetString(GameSceneLoader.EditorPendingSlotKey, latest.slotId);
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != GameScenes.Game)
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        EditorApplication.isPlaying = true;
    }

    [MenuItem("TCG Card Caos/Save/Open Save Folder")]
    public static void OpenSaveFolder()
    {
        SaveFileIO.EnsureRoot();
        EditorUtility.RevealInFinder(SaveFileIO.RootFolder);
    }

    [MenuItem("TCG Card Caos/Save/List Slots")]
    public static void ListSlots()
    {
        var slots = GameSaveStore.GetSaveSlots();
        if (slots.Count == 0)
        {
            Debug.Log("[Save] No compatible slots in " + SaveFileIO.RootFolder);
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            SaveSlotMetadata slot = slots[i];
            Debug.Log(
                "[Save] " + slot.slotId
                + " type=" + slot.slotType
                + " time=" + slot.TimestampUtc.ToString("u")
                + " play=" + slot.playTimeSeconds.ToString("0.0")
                + " cards=" + slot.cardsPlaced + "/" + slot.totalCards
                + " shelves=" + slot.shelvesCompleted + "/" + slot.totalShelves);
        }
    }
}

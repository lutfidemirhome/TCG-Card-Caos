using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
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

    [MenuItem("TCG Card Caos/Save/Steam Demo Checklist")]
    public static void ShowSteamDemoChecklist()
    {
        SaveFileIO.CacheRootOnMainThread();
        string message =
            "Demo'yu sonra komple yapacaksın. O gün senin işlerin:\n\n"
            + "1. Steamworks'te ayrı Demo App ID al. Full ile aynı ID kullanma.\n"
            + "2. Sadece demo build'de Player Settings → Scripting Define Symbols'a TCG_DEMO yaz. Full build'e yazma.\n"
            + "3. İstersen Steam Cloud Auto-Cloud bağla.\n"
            + "   Demo klasör: .../TCGCardChaos_Demo/\n"
            + "   Full klasör: .../TCGCardChaos/\n"
            + "   Dosyalar: *.json, *.meta.json, *.png, manifest.json\n"
            + "4. Steamworks SDK (overlay, achievement) save'den ayrı. Save için şart değil.\n"
            + "5. İstersen demo product name: TCG Card Caos Demo\n\n"
            + "Şu an variant: " + GameBuildVariant.Current
            + "\nKayıt klasörü: " + SaveFileIO.RootFolder;

        EditorUtility.DisplayDialog("Steam Demo — senin işlerin", message, "Tamam");
        Debug.Log("[Save] Steam Demo checklist\n" + message);
    }

    [MenuItem("TCG Card Caos/Save/Wipe Local Saves")]
    public static void WipeLocalSavesMenu()
    {
        int removed = WipeLocalSaveFolders();
        EditorUtility.DisplayDialog(
            "Save",
            removed > 0
                ? "Local save folders were deleted. Next play/build starts with no saves."
                : "No local save folders were found.",
            "OK");
    }

    public static int WipeLocalSaveFolders()
    {
        string parent = Application.persistentDataPath;
        string[] folders =
        {
            Path.Combine(parent, GameBuildVariant.FullFolderName),
            Path.Combine(parent, GameBuildVariant.DemoFolderName)
        };

        int removed = 0;
        for (int i = 0; i < folders.Length; i++)
        {
            string folder = folders[i];
            if (!Directory.Exists(folder))
                continue;

            try
            {
                Directory.Delete(folder, true);
                removed++;
                Debug.Log("[Save] Wiped " + folder);
            }
            catch (IOException exception)
            {
                Debug.LogWarning("[Save] Could not wipe " + folder + ": " + exception.Message);
            }
        }

        return removed;
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

class WipeSavesOnBuild : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        GameSaveEditorMenu.WipeLocalSaveFolders();
    }
}

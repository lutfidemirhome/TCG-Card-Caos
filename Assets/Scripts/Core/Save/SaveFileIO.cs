using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Atomic JSON write + metadata listing. No UnityEngine.Object access on worker threads.
/// </summary>
public static class SaveFileIO
{
    const string ManifestFileName = "manifest.json";
    const string TmpSuffix = ".tmp";
    const string BackupSuffix = ".bak";

    static string _rootFolder;

    public static string RootFolder
    {
        get
        {
            if (string.IsNullOrEmpty(_rootFolder))
                CacheRootOnMainThread();
            return _rootFolder;
        }
    }

    public static void CacheRootOnMainThread()
    {
        _rootFolder = Path.Combine(Application.persistentDataPath, GameBuildVariant.FolderName);
    }

    public static string GetSavePath(string slotId) => Path.Combine(RootFolder, slotId + ".json");
    public static string GetMetaPath(string slotId) => Path.Combine(RootFolder, slotId + ".meta.json");
    public static string GetThumbnailPath(string slotId) => Path.Combine(RootFolder, slotId + ".png");
    public static string GetBackupPath(string slotId) => Path.Combine(RootFolder, slotId + BackupSuffix);

    public static string AutosaveSlotId(int index) => "autosave_" + index;
    public static string ManualSlotId(int index) => "manual_" + index;

    public static void EnsureRoot()
    {
        Directory.CreateDirectory(RootFolder);
    }

    public static bool TryWriteAtomic(string finalPath, string json, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(finalPath) || json == null)
        {
            error = "Invalid save path or payload.";
            return false;
        }

        EnsureRoot();
        string tempPath = finalPath + TmpSuffix;
        string backupPath = finalPath + BackupSuffix;

        try
        {
            File.WriteAllText(tempPath, json);
            using (FileStream stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                stream.Flush(true);

            if (File.Exists(finalPath))
                File.Copy(finalPath, backupPath, overwrite: true);

            if (File.Exists(finalPath))
                File.Delete(finalPath);
            File.Move(tempPath, finalPath);

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            TryDelete(tempPath);
            return false;
        }
    }

    public static bool TryReadText(string path, out string text, out string error)
    {
        text = null;
        error = null;
        try
        {
            if (!File.Exists(path))
            {
                error = "File missing.";
                return false;
            }

            text = File.ReadAllText(path);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static bool TryLoadSave(string slotId, out GameSaveData data, out string error)
    {
        data = null;
        if (TryReadValidatedSave(GetSavePath(slotId), out data, out error))
            return true;

        return TryReadValidatedSave(GetBackupPath(slotId), out data, out error);
    }

    static bool TryReadValidatedSave(string path, out GameSaveData data, out string error)
    {
        data = null;
        if (!TryReadText(path, out string json, out error))
            return false;

        try
        {
            data = JsonUtility.FromJson<GameSaveData>(json);
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }

        return ValidateSave(data, out error);
    }

    public static bool ValidateSave(GameSaveData data, out string error)
    {
        error = null;
        if (data == null)
        {
            error = "Save is empty.";
            return false;
        }

        if (data.saveVersion > GameSaveSettings.CurrentSaveVersion)
        {
            error = "Save is from a newer game version.";
            return false;
        }

        if (data.saveVersion < 1)
        {
            error = "Save version is invalid.";
            return false;
        }

        if (!GameBuildVariant.Matches(data.buildVariant))
        {
            error = "Save belongs to a different build variant.";
            return false;
        }

        if (!string.Equals(data.worldId, GameScenes.Game, StringComparison.Ordinal))
        {
            error = "Save belongs to a different world.";
            return false;
        }

        MigrateIfNeeded(data);
        return true;
    }

    public static bool ValidateMetadata(SaveSlotMetadata metadata)
    {
        return metadata != null
               && metadata.isValid
               && metadata.saveVersion <= GameSaveSettings.CurrentSaveVersion
               && metadata.saveVersion >= 1
               && GameBuildVariant.Matches(metadata.buildVariant)
               && string.Equals(metadata.worldId, GameScenes.Game, StringComparison.Ordinal);
    }

    static void MigrateIfNeeded(GameSaveData data)
    {
        if (data.saveVersion < GameSaveSettings.CurrentSaveVersion)
            data.saveVersion = GameSaveSettings.CurrentSaveVersion;
    }

    public static bool TryLoadMetadata(string slotId, out SaveSlotMetadata metadata)
    {
        metadata = null;
        if (TryReadText(GetMetaPath(slotId), out string json, out _) && !string.IsNullOrEmpty(json))
        {
            try
            {
                metadata = JsonUtility.FromJson<SaveSlotMetadata>(json);
            }
            catch
            {
                metadata = null;
            }
        }

        if (metadata != null)
        {
            metadata.slotId = slotId;
            metadata.thumbnailAvailable = metadata.thumbnailAvailable && File.Exists(GetThumbnailPath(slotId));
            metadata.isValid = ValidateMetadata(metadata);
            return metadata.isValid;
        }

        if (TryLoadSave(slotId, out GameSaveData save, out _))
        {
            metadata = save.ToMetadata(File.Exists(GetThumbnailPath(slotId)));
            return true;
        }

        return false;
    }

    public static List<SaveSlotMetadata> ListCompatibleSlots()
    {
        var results = new List<SaveSlotMetadata>(12);
        if (!Directory.Exists(RootFolder))
            return results;

        string[] files = Directory.GetFiles(RootFolder, "*.meta.json");
        for (int i = 0; i < files.Length; i++)
        {
            string slotId = Path.GetFileName(files[i]).Replace(".meta.json", string.Empty);
            if (TryLoadMetadata(slotId, out SaveSlotMetadata metadata))
                results.Add(metadata);
        }

        if (results.Count == 0)
        {
            string[] saves = Directory.GetFiles(RootFolder, "*.json");
            for (int i = 0; i < saves.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(saves[i]);
                if (name == "manifest" || name.EndsWith(".meta", StringComparison.Ordinal))
                    continue;
                if (TryLoadMetadata(name, out SaveSlotMetadata metadata))
                    results.Add(metadata);
            }
        }

        results.Sort(CompareNewestFirst);
        return results;
    }

    static int CompareNewestFirst(SaveSlotMetadata a, SaveSlotMetadata b)
    {
        return b.timestampUnix.CompareTo(a.timestampUnix);
    }

    public static SaveManifest LoadManifest()
    {
        string path = Path.Combine(RootFolder, ManifestFileName);
        if (TryReadText(path, out string json, out _) && !string.IsNullOrEmpty(json))
        {
            try
            {
                SaveManifest loaded = JsonUtility.FromJson<SaveManifest>(json);
                if (loaded != null)
                    return loaded;
            }
            catch
            {
                // Fall through to a fresh manifest.
            }
        }

        return new SaveManifest();
    }

    public static void WriteManifest(SaveManifest manifest)
    {
        if (manifest == null)
            return;

        EnsureRoot();
        TryWriteAtomic(
            Path.Combine(RootFolder, ManifestFileName),
            JsonUtility.ToJson(manifest, false),
            out _);
    }

    public static bool TryWriteSaveAndMeta(GameSaveData data, SaveSlotMetadata metadata, out string error)
    {
        error = null;
        if (data == null || metadata == null)
        {
            error = "Missing save payload.";
            return false;
        }

        string json = JsonUtility.ToJson(data, false);
        if (!TryWriteAtomic(GetSavePath(data.slotId), json, out error))
            return false;

        string metaJson = JsonUtility.ToJson(metadata, false);
        if (!TryWriteAtomic(GetMetaPath(data.slotId), metaJson, out string metaError))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[Save] Metadata write failed: " + metaError);
#endif
        }

        return true;
    }

    public static bool TryWriteThumbnail(string slotId, byte[] pngBytes, out string error)
    {
        error = null;
        if (pngBytes == null || pngBytes.Length == 0)
        {
            error = "Empty thumbnail.";
            return false;
        }

        try
        {
            EnsureRoot();
            File.WriteAllBytes(GetThumbnailPath(slotId), pngBytes);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static void DeleteSlot(string slotId)
    {
        TryDelete(GetSavePath(slotId));
        TryDelete(GetMetaPath(slotId));
        TryDelete(GetThumbnailPath(slotId));
        TryDelete(GetBackupPath(slotId));
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Non-fatal.
        }
    }
}

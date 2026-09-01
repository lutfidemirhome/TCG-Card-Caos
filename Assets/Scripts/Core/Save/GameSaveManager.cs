using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Central save coordinator. Gameplay talks to this and <see cref="GameSaveSignals"/> only.
/// </summary>
public sealed class GameSaveManager : MonoBehaviour
{
    public enum SaveRequestKind
    {
        Autosave,
        Milestone,
        Exit,
        Manual,
    }

    static GameSaveManager _instance;
    static bool _milestoneQueued;
    static bool _autosaveQueued;
    static bool _manualQueued;
    static string _queuedManualSlotId;

    GameSaveSettings _settings;
    float _periodicTimer;
    bool _saveInProgress;
    bool _sessionStarted;
    Coroutine _thumbnailRoutine;
    string _activeThumbnailSlot;

    public static GameSaveManager Instance => _instance;

    public static GameSaveManager EnsureExists()
    {
        if (_instance != null)
            return _instance;

        var existing = FindFirstObjectByType<GameSaveManager>();
        if (existing != null)
        {
            _instance = existing;
            return _instance;
        }

        var root = new GameObject(nameof(GameSaveManager));
        DontDestroyOnLoad(root);
        _instance = root.AddComponent<GameSaveManager>();
        return _instance;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance = null;
        _milestoneQueued = false;
        _autosaveQueued = false;
        _manualQueued = false;
        _queuedManualSlotId = null;
        GamePlayTime.Reset();
        GameSaveDirtyTracker.Clear();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        _settings = GameSaveSettings.LoadOrDefault();
        SaveFileIO.CacheRootOnMainThread();
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void Update()
    {
        if (!GameScenes.IsActiveGameScene())
            return;

        if (Time.timeScale > 0f)
            GamePlayTime.Tick(Time.deltaTime);

        if (!GamePause.IsPaused && Input.GetKeyDown(KeyCode.O))
        {
            int filled = CabinetFillDebug.FillCabinets(CabinetFillDebug.DefaultCabinetCount);
            Debug.Log("[Debug] O: filled " + filled + " / " + CabinetFillDebug.DefaultCabinetCount + " cabinets.");
        }

        _periodicTimer += Time.unscaledDeltaTime;
        if (_periodicTimer >= _settings.PeriodicAutosaveSeconds)
        {
            _periodicTimer = 0f;
            if (GameSaveDirtyTracker.IsDirty)
                RequestAutosave(SaveRequestKind.Autosave);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (WasSaveDebugPressed(KeyCode.F6, KeyCode.Alpha6))
            RequestAutosave(SaveRequestKind.Autosave);
        if (WasSaveDebugPressed(KeyCode.F7, KeyCode.Alpha7))
            LoadLatestFromGameplay();
        if (WasSaveDebugPressed(KeyCode.F8, KeyCode.Alpha8))
            Application.OpenURL("file://" + SaveFileIO.RootFolder);
#endif
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
            TryExitSave();
    }

    void OnApplicationQuit()
    {
        TryExitSave();
    }

    static bool WasSaveDebugPressed(KeyCode functionKey, KeyCode numberKey)
    {
        if (Input.GetKeyDown(functionKey))
            return true;

        return (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
               && Input.GetKeyDown(numberKey);
    }

    public void BeginGameplaySession(bool fromSave, double loadedPlayTime)
    {
        _sessionStarted = true;
        _periodicTimer = 0f;
        if (fromSave)
            GamePlayTime.BeginSession(loadedPlayTime);
        else
            GamePlayTime.BeginSession(0d);
    }

    public static void NotifyNewGameWorldReady()
    {
        EnsureExists();
        GameSaveDirtyTracker.Clear();
        GameProgressCounter.InvalidateCache();
        _instance.BeginGameplaySession(fromSave: false, 0d);
    }

    public static void NotifySaveRestored()
    {
        EnsureExists();
        GameProgressCounter.InvalidateCache();
        _instance.BeginGameplaySession(fromSave: true, GamePlayTime.TotalSeconds);
    }

    public static void RequestMilestoneAutosave()
    {
        if (_instance == null)
        {
            _milestoneQueued = true;
            return;
        }

        _instance.RequestAutosave(SaveRequestKind.Milestone);
    }

    public void RequestAutosave(SaveRequestKind kind)
    {
        if (!GameScenes.IsActiveGameScene())
            return;

        if (_saveInProgress)
        {
            if (kind == SaveRequestKind.Manual)
            {
                _manualQueued = true;
                _queuedManualSlotId = null;
            }
            else
            {
                _autosaveQueued = true;
            }

            return;
        }

        if (kind != SaveRequestKind.Manual && kind != SaveRequestKind.Exit && !GameSaveDirtyTracker.IsDirty)
            return;

        StartCoroutine(CommitRoutine(kind, manualSlotId: null));
    }

    public void ForceAutosaveNow()
    {
        if (!GameScenes.IsActiveGameScene())
        {
            Debug.LogWarning("[Save] Autosave skipped: MainScene is not active.");
            return;
        }

        GameSaveDirtyTracker.MarkDirty();
        if (_saveInProgress)
        {
            _autosaveQueued = true;
            Debug.Log("[Save] Autosave queued; wait a moment.");
            return;
        }

        CommitSynchronous(SaveRequestKind.Autosave, null);
    }

    public void SaveManual(string slotId = null)
    {
        if (_saveInProgress)
        {
            _manualQueued = true;
            _queuedManualSlotId = slotId;
            return;
        }

        StartCoroutine(CommitRoutine(SaveRequestKind.Manual, slotId));
    }

    public void SaveAndQuit()
    {
        TryExitSave();
        Application.Quit();
    }

    public void SaveBeforeLeaveGameplay()
    {
        TryExitSave();
    }

    void TryExitSave()
    {
        if (!GameScenes.IsActiveGameScene() || !_sessionStarted)
            return;
        if (!GameSaveDirtyTracker.IsDirty && !HasAnyCompatibleSave())
            return;
        if (!GameSaveDirtyTracker.IsDirty)
            return;
        if (_saveInProgress)
        {
            _autosaveQueued = true;
            return;
        }

        CommitSynchronous(SaveRequestKind.Exit, null);
    }

    IEnumerator CommitRoutine(SaveRequestKind kind, string manualSlotId)
    {
        _saveInProgress = true;
        float collectMs = 0f;
        float serializeMs = 0f;
        float writeMs = 0f;
        string error = null;
        GameSaveData data = null;
        SaveSlotMetadata metadata = null;

        ResolveSlot(kind, manualSlotId, out string slotId, out SaveSlotType slotType, out int slotIndex);
        GameSaveEvents.RaiseSaveStarted(slotId);

        float start = Time.realtimeSinceStartup;
        data = GameSaveWorldCollector.Collect(slotId, slotType, slotIndex);
        collectMs = (Time.realtimeSinceStartup - start) * 1000f;

        start = Time.realtimeSinceStartup;
        string json = JsonUtility.ToJson(data, false);
        metadata = data.ToMetadata(false);
        string metaJson = JsonUtility.ToJson(metadata, false);
        serializeMs = (Time.realtimeSinceStartup - start) * 1000f;

        SaveFileIO.CacheRootOnMainThread();
        string savePath = SaveFileIO.GetSavePath(data.slotId);
        string metaPath = SaveFileIO.GetMetaPath(data.slotId);

        bool writeOk = false;
        Task writeTask = Task.Run(() =>
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            writeOk = SaveFileIO.TryWriteAtomic(savePath, json, out error);
            if (writeOk)
                SaveFileIO.TryWriteAtomic(metaPath, metaJson, out _);
            watch.Stop();
            writeMs = (float)watch.Elapsed.TotalMilliseconds;
        });

        while (!writeTask.IsCompleted)
            yield return null;

        if (writeTask.IsFaulted)
        {
            error = writeTask.Exception != null
                ? writeTask.Exception.GetBaseException().Message
                : "Save task failed.";
            writeOk = false;
        }

        if (writeOk)
        {
            GameSaveDirtyTracker.Clear();
            if (kind == SaveRequestKind.Autosave || kind == SaveRequestKind.Milestone || kind == SaveRequestKind.Exit)
                AdvanceAutosaveIndex(slotIndex);

            GameSaveEvents.RaiseSaveCompleted(metadata);
            LogSave(kind, slotId, data, collectMs, serializeMs, writeMs);
            BeginThumbnail(slotId);
        }
        else
        {
            GameSaveEvents.RaiseSaveFailed(error ?? "Save failed.");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[Save] Failed (" + kind + "): " + error);
#endif
        }

        _saveInProgress = false;
        DrainQueue();
    }

    void CommitSynchronous(SaveRequestKind kind, string manualSlotId)
    {
        _saveInProgress = true;
        ResolveSlot(kind, manualSlotId, out string slotId, out SaveSlotType slotType, out int slotIndex);
        GameSaveEvents.RaiseSaveStarted(slotId);

        try
        {
            GameSaveData data = GameSaveWorldCollector.Collect(slotId, slotType, slotIndex);
            SaveSlotMetadata metadata = data.ToMetadata(false);
            if (SaveFileIO.TryWriteSaveAndMeta(data, metadata, out string error))
            {
                GameSaveDirtyTracker.Clear();
                if (kind != SaveRequestKind.Manual)
                    AdvanceAutosaveIndex(slotIndex);
                GameSaveEvents.RaiseSaveCompleted(metadata);
                LogSave(kind, slotId, data, 0f, 0f, 0f);
            }
            else
            {
                GameSaveEvents.RaiseSaveFailed(error);
            }
        }
        catch (Exception exception)
        {
            GameSaveEvents.RaiseSaveFailed(exception.Message);
        }
        finally
        {
            _saveInProgress = false;
        }
    }

    void DrainQueue()
    {
        if (_manualQueued)
        {
            _manualQueued = false;
            string slotId = _queuedManualSlotId;
            _queuedManualSlotId = null;
            SaveManual(slotId);
            return;
        }

        if (_milestoneQueued || _autosaveQueued)
        {
            bool milestone = _milestoneQueued;
            _milestoneQueued = false;
            _autosaveQueued = false;
            RequestAutosave(milestone ? SaveRequestKind.Milestone : SaveRequestKind.Autosave);
        }
    }

    void ResolveSlot(SaveRequestKind kind, string manualSlotId, out string slotId, out SaveSlotType slotType, out int slotIndex)
    {
        if (kind == SaveRequestKind.Manual)
        {
            if (!string.IsNullOrEmpty(manualSlotId))
            {
                slotId = manualSlotId;
                slotIndex = ParseIndex(manualSlotId);
                slotType = manualSlotId.StartsWith("autosave_", StringComparison.Ordinal)
                    ? SaveSlotType.Auto
                    : SaveSlotType.Manual;
                return;
            }

            slotType = SaveSlotType.Manual;

            slotIndex = NextManualIndex();
            slotId = SaveFileIO.ManualSlotId(slotIndex);
            return;
        }

        slotType = SaveSlotType.Auto;
        slotIndex = Mathf.Clamp(SaveFileIO.LoadManifest().nextAutosaveIndex, 0, GameSaveSettings.AutosaveSlotCount - 1);
        slotId = SaveFileIO.AutosaveSlotId(slotIndex);
    }

    static int ParseIndex(string slotId)
    {
        int underscore = slotId.LastIndexOf('_');
        if (underscore < 0 || underscore == slotId.Length - 1)
            return 0;
        return int.TryParse(slotId.Substring(underscore + 1), out int index) ? index : 0;
    }

    int NextManualIndex()
    {
        List<SaveSlotMetadata> slots = SaveFileIO.ListCompatibleSlots();
        int used = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].slotType == SaveSlotType.Manual)
                used = Mathf.Max(used, slots[i].slotIndex + 1);
        }

        return used % Mathf.Max(1, _settings.MaxManualSlots);
    }

    static void AdvanceAutosaveIndex(int writtenIndex)
    {
        SaveManifest manifest = SaveFileIO.LoadManifest();
        manifest.nextAutosaveIndex = (writtenIndex + 1) % GameSaveSettings.AutosaveSlotCount;
        SaveFileIO.WriteManifest(manifest);
    }

    void BeginThumbnail(string slotId)
    {
        if (_thumbnailRoutine != null && _activeThumbnailSlot == slotId)
            StopCoroutine(_thumbnailRoutine);

        _activeThumbnailSlot = slotId;
        _thumbnailRoutine = StartCoroutine(ThumbnailWrapper(slotId));
    }

    IEnumerator ThumbnailWrapper(string slotId)
    {
        yield return GameSaveThumbnail.CaptureRoutine(slotId, _settings);
        if (_activeThumbnailSlot == slotId)
        {
            _activeThumbnailSlot = null;
            _thumbnailRoutine = null;
        }
    }

    void LogSave(SaveRequestKind kind, string slotId, GameSaveData data, float collectMs, float serializeMs, float writeMs)
    {
        int shelfCards = 0;
        int psaCards = 0;
        if (data != null && data.cards != null)
        {
            for (int i = 0; i < data.cards.Length; i++)
            {
                CardSaveRecord card = data.cards[i];
                if (card == null)
                    continue;
                if (card.location == CardRuntimeLocation.Shelf)
                    shelfCards++;
                else if (card.location == CardRuntimeLocation.PsaCabinet)
                    psaCards++;
            }
        }

        Debug.Log(
            "[Save] Completed " + kind + " " + slotId
            + " shelf=" + shelfCards
            + " psa=" + psaCards
            + " total=" + (data != null && data.cards != null ? data.cards.Length : 0));
    }

    void LoadLatestFromGameplay()
    {
        SaveSlotMetadata latest = GetLatestValidSave();
        if (latest == null)
        {
            Debug.LogWarning("[Save] No valid save to load.");
            return;
        }

        GameSceneLoader.LoadSaveSlot(latest.slotId);
    }

    public static bool HasAnyCompatibleSave()
    {
        return GetLatestValidSave() != null;
    }

    public static List<SaveSlotMetadata> GetSaveSlots()
    {
        SaveFileIO.CacheRootOnMainThread();
        return SaveFileIO.ListCompatibleSlots();
    }

    public static SaveSlotMetadata GetLatestValidSave()
    {
        List<SaveSlotMetadata> slots = SaveFileIO.ListCompatibleSlots();
        if (slots.Count == 0)
            return null;

        SaveSlotMetadata newest = slots[0];
        if (newest.cardsPlaced > 0 || newest.playTimeSeconds >= 30d)
            return newest;

        for (int i = 1; i < slots.Count; i++)
        {
            if (slots[i].cardsPlaced > 0)
                return slots[i];
        }

        return newest;
    }

    public static void DeleteSaveSlot(string slotId)
    {
        SaveFileIO.DeleteSlot(slotId);
    }

    public static void CreateNewGame()
    {
        GameSceneLoader.StartNewGame();
    }

    public static void LoadSaveSlot(string slotId)
    {
        GameSceneLoader.LoadSaveSlot(slotId);
    }

    public static bool TryRestorePending(out string error)
    {
        error = null;
        return true;
    }
}

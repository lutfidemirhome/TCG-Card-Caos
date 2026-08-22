using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads the gameplay scene behind a loading overlay and waits until cards/bootstrap are ready.
/// </summary>
public class GameSceneLoader : MonoBehaviour
{
    public const string EditorPendingSlotKey = "TCGCardCaos.PendingSaveSlot";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _runner = null;
        _isLoading = false;
        _startedViaMenuLoader = false;
        _pendingLoadMode = GameLoadMode.NewGame;
        _pendingSlotId = null;
    }

    static GameSceneLoader _runner;
    static bool _isLoading;
    static bool _startedViaMenuLoader;
    static GameLoadMode _pendingLoadMode = GameLoadMode.NewGame;
    static string _pendingSlotId;

    public static bool StartedViaMenuLoader => _startedViaMenuLoader;

    public static bool IsLoading => _isLoading;

    public static GameLoadMode PendingLoadMode
    {
        get
        {
            ConsumeEditorPendingIfNeeded();
            return _pendingLoadMode;
        }
    }

    public static string PendingSlotId
    {
        get
        {
            ConsumeEditorPendingIfNeeded();
            return _pendingSlotId;
        }
    }

    public static void StartNewGame()
    {
        _pendingLoadMode = GameLoadMode.NewGame;
        _pendingSlotId = null;
        LoadGame();
    }

    public static void ContinueGame()
    {
        SaveSlotMetadata latest = GameSaveManager.GetLatestValidSave();
        if (latest == null)
            return;

        _pendingLoadMode = GameLoadMode.Continue;
        _pendingSlotId = latest.slotId;
        LoadGame();
    }

    public static void LoadSaveSlot(string slotId)
    {
        if (string.IsNullOrEmpty(slotId) || !SaveFileIO.TryLoadMetadata(slotId, out _))
            return;

        _pendingLoadMode = GameLoadMode.LoadSlot;
        _pendingSlotId = slotId;
        LoadGame();
    }

    public static void ClearPendingLoad()
    {
        _pendingLoadMode = GameLoadMode.NewGame;
        _pendingSlotId = null;
        _startedViaMenuLoader = false;
    }

    public static void LoadGame()
    {
        if (_isLoading || !Application.isPlaying)
            return;

        _isLoading = true;
        _startedViaMenuLoader = true;

        LoadingScreenUI loadingScreen = LoadingScreenUI.Ensure();
        loadingScreen.Show();

        InGamePauseView pause = Object.FindFirstObjectByType<InGamePauseView>();
        if (pause != null)
            pause.Hide();

        GamePause.SetPaused(false);
        EnsureRunner().StartCoroutine(LoadGameRoutine(loadingScreen));
    }

    static void ConsumeEditorPendingIfNeeded()
    {
#if UNITY_EDITOR
        string slotId = UnityEditor.SessionState.GetString(EditorPendingSlotKey, string.Empty);
        if (string.IsNullOrEmpty(slotId))
            return;

        UnityEditor.SessionState.EraseString(EditorPendingSlotKey);
        _pendingLoadMode = GameLoadMode.LoadSlot;
        _pendingSlotId = slotId;
#endif
    }

    static GameSceneLoader EnsureRunner()
    {
        if (_runner != null)
            return _runner;

        var root = new GameObject(nameof(GameSceneLoader));
        if (Application.isPlaying)
            DontDestroyOnLoad(root);
        _runner = root.AddComponent<GameSceneLoader>();
        return _runner;
    }

    static IEnumerator LoadGameRoutine(LoadingScreenUI loadingScreen)
    {
        if (loadingScreen == null)
            loadingScreen = LoadingScreenUI.Ensure();

        loadingScreen.Show();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        yield return null;

        GameAssetPrewarm.EnsureReady();
        GamePlayBootstrap.PrepareForSceneReload();
        CardInstancedRenderManager.ResetGameplayReady();

        ThreadPriority previousPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.Low;

        bool reloadActiveGame = GameScenes.IsActiveGameScene();
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(GameScenes.Game, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            Application.backgroundLoadingPriority = previousPriority;
            Debug.LogError("GameSceneLoader: Failed to start loading " + GameScenes.Game + ".");
            loadingScreen.Hide();
            _isLoading = false;
            yield break;
        }

        // Reloading the scene you are already in deadlocks if activation is held at 0.9.
        if (reloadActiveGame)
        {
            loadOperation.allowSceneActivation = true;
        }
        else
        {
            loadOperation.allowSceneActivation = false;
            while (loadOperation.progress < 0.9f)
                yield return null;

            loadOperation.allowSceneActivation = true;
        }

        while (!loadOperation.isDone)
            yield return null;

        Application.backgroundLoadingPriority = previousPriority;

        while (!CardInstancedRenderManager.IsGameplayReady)
            yield return null;

        yield return null;
        loadingScreen.Hide();
        _isLoading = false;
    }

    void OnDestroy()
    {
        if (_runner == this)
            _runner = null;
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures player hand systems and test cards exist when the gameplay scene loads.
/// AfterSceneLoad only runs for the first scene; sceneLoaded handles menu → game transitions.
/// </summary>
static class GamePlayBootstrap
{
    static int _bootstrappedSceneHandle = -1;
    static bool _sceneHookRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _sceneHookRegistered = false;
        _bootstrappedSceneHandle = -1;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InitializeFirstScene()
    {
        EnsureSceneHook();
        TryBootstrap(SceneManager.GetActiveScene());
    }

    static void EnsureSceneHook()
    {
        if (_sceneHookRegistered)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        _sceneHookRegistered = true;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryBootstrap(scene);
    }

    static void TryBootstrap(Scene scene)
    {
        if (!GameScenes.IsGameScene(scene) || !scene.isLoaded)
            return;

        if (_bootstrappedSceneHandle == scene.handle)
            return;

        _bootstrappedSceneHandle = scene.handle;

        CardInstancedRenderManager.BeginBulkGroundLoad();
        GameSaveManager.EnsureExists();
        EnsureCameraSystems();
        EnsurePlayerHand();
        StoreLighting.EnsureExists();
        BackgroundMusic.EnsureExists();
        GameSoundEffects.EnsureExists();
        CardVisualResources.ApplyOutlineSettings(
            Resources.Load<CardOutlineSettings>(CardOutlineSettings.ResourcePath));
        CardInstancedRenderManager.EnsureExists().SchedulePlayModeSetup();
    }

    static void EnsureCameraSystems()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        UiEventSystem.Ensure();

        if (camera.GetComponent<CrosshairUI>() == null)
            camera.gameObject.AddComponent<CrosshairUI>();

        if (camera.GetComponent<InteractionController>() == null)
            camera.gameObject.AddComponent<InteractionController>();

        CardInspectPreview.EnsureOn(camera);
        PackInspectPreview.EnsureOn(camera);
        PsaInspectPreview.EnsureOn(camera);
    }

    static void EnsurePlayerHand()
    {
        FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>();
        if (player == null)
            return;

        if (player.GetComponent<PlayerCardHand>() == null)
            player.gameObject.AddComponent<PlayerCardHand>();
    }
}

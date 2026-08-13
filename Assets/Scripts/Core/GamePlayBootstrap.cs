using UnityEngine;

/// <summary>
/// Ensures player hand systems and test cards exist in Play mode.
/// Heavy card setup runs asynchronously so Play mode opens immediately.
/// </summary>
static class GamePlayBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void BeforeSceneLoad()
    {
        CardInstancedRenderManager.BeginBulkGroundLoad();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        EnsureCameraSystems();
        EnsurePlayerHand();
        StoreLighting.EnsureExists();
        BackgroundMusic.EnsureExists();
        GameSoundEffects.EnsureExists();
        CardInstancedRenderManager.EnsureExists().SchedulePlayModeSetup();
    }

    static void EnsureCameraSystems()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        if (camera.GetComponent<CrosshairUI>() == null)
            camera.gameObject.AddComponent<CrosshairUI>();

        if (camera.GetComponent<InteractionController>() == null)
            camera.gameObject.AddComponent<InteractionController>();

        CardInspectPreview.EnsureOn(camera);
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

using UnityEngine;

/// <summary>
/// Ensures player hand systems and test cards exist in Play mode.
/// </summary>
static class GamePlayBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        EnsureCameraSystems();
        EnsurePlayerHand();
        EnsureCardInstancedRenderer();
        EnsureTestCards();
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

    static void EnsureCardInstancedRenderer()
    {
        CardInstancedRenderManager.EnsureExists();
    }

    static void EnsureTestCards()
    {
        if (Object.FindFirstObjectByType<WorldCard>() != null)
        {
            CardScatterUtility.SnapCardsToFloor();
            return;
        }

        CardScatterUtility.SpawnScatteredCards();
    }
}

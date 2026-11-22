using UnityEngine;

/// <summary>
/// Shared pause flag for gameplay. UI sets this; player/interaction systems read it.
/// </summary>
public static class GamePause
{
    public static bool IsPaused { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    public static void SetPaused(bool paused)
    {
        IsPaused = paused;
        // Opening a personal menu must not freeze the host's shared card physics or shelf flights.
        Time.timeScale = paused && !CoopSession.InShop ? 0f : 1f;
        AudioListener.pause = paused;
    }
}

using UnityEngine;

/// <summary>
/// Applies the player's frame limit independently of visual quality. Uses display sync
/// when the selected rate divides the actual refresh rate, and a software cap otherwise.
/// </summary>
public sealed class GameFramePacing : MonoBehaviour
{
    static GameFramePacing _instance;
    double _nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _instance = null;

    internal static void Ensure()
    {
        if (!Application.isPlaying || _instance != null)
            return;

        var host = new GameObject("Frame pacing");
        _instance = host.AddComponent<GameFramePacing>();
        DontDestroyOnLoad(host);
        Apply(GameSettings.Current.frameRateLimit);
    }

    void Update()
    {
        // Windows can change display refresh when switching to battery or another monitor.
        // Screen.SetResolution is also asynchronous, so re-evaluate after it completes.
        if (Time.unscaledTimeAsDouble < _nextCheck)
            return;
        _nextCheck = Time.unscaledTimeAsDouble + 1d;
        Apply(GameSettings.Current.frameRateLimit);
    }

    internal static void Apply(GameSettings.FrameRateLimit limit)
    {
        if (!Application.isPlaying)
            return;

        int sync, target;
        Resolve(limit, Screen.currentResolution.refreshRateRatio.value, out sync, out target);
        if (QualitySettings.vSyncCount != sync)
            QualitySettings.vSyncCount = sync;
        if (Application.targetFrameRate != target)
            Application.targetFrameRate = target;
    }

    internal static void Resolve(GameSettings.FrameRateLimit limit, double refreshHz, out int sync, out int target)
    {
        int requested = limit == GameSettings.FrameRateLimit.Fps30 ? 30
            : limit == GameSettings.FrameRateLimit.Fps60 ? 60 : 0;
        bool validRefresh = !double.IsNaN(refreshHz) && !double.IsInfinity(refreshHz) && refreshHz > 0d;
        if (!validRefresh)
        {
            sync = 0;
            target = requested > 0 ? requested : 60;
            return;
        }

        if (requested == 0 || refreshHz <= requested + 0.15d)
        {
            sync = 1;
            target = -1;
            return;
        }

        for (int divisor = 2; divisor <= 4; divisor++)
        {
            // Treat 59.94/119.88 Hz displays as their nominal 60/120 Hz equivalents.
            if (System.Math.Abs(refreshHz / divisor - requested) <= 0.15d)
            {
                sync = divisor;
                target = -1;
                return;
            }
        }

        // Unity ignores targetFrameRate with VSync enabled. Do not accidentally cap
        // a 144 Hz display to 72 or 48 when the player explicitly selected 60 FPS.
        sync = 0;
        target = requested;
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}

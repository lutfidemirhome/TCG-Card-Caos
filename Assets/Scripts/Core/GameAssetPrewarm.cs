using System.Collections;
using UnityEngine;

/// <summary>
/// Preloads heavy gameplay assets while the main menu is visible.
/// </summary>
public static class GameAssetPrewarm
{
    static bool _started;
    static bool _complete;

    public static bool IsComplete => _complete;

    /// <summary>
    /// In-game load never went through the menu prewarm. Don't block the loading screen forever.
    /// </summary>
    public static void EnsureReady()
    {
        if (_complete)
            return;

        CardArtLibrary.EnsureLoaded();
        CardCatalog.EnsureLoaded();
        _started = true;
        _complete = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _started = false;
        _complete = false;
    }

    public static void Start(MonoBehaviour host)
    {
        if (_started || host == null)
            return;

        _started = true;
        host.StartCoroutine(PrewarmRoutine());
    }

    static IEnumerator PrewarmRoutine()
    {
        yield return null;
        CardCatalog.EnsureLoaded();
        yield return null;
        CardArtLibrary.EnsureLoaded();
        _complete = true;
    }
}

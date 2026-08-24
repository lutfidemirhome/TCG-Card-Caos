using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shows the existing loading overlay (plus fiction disclaimer) every time the process starts
/// and the main menu is loading. Skipped when Play starts directly in the gameplay scene.
/// </summary>
public static class GameBootLoader
{
    const float MinDisplaySeconds = 10f;

    static bool _started;
    static Host _host;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _started = false;
        _host = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ShowOverlayBeforeFirstFrame()
    {
        if (!Application.isPlaying || IsDirectGameplayLaunch())
            return;

        LoadingScreenUI loadingScreen = LoadingScreenUI.Ensure();
        if (loadingScreen != null)
            loadingScreen.ShowBoot();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnFirstSceneLoaded()
    {
        if (_started || !Application.isPlaying)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || GameScenes.IsGameScene(scene))
        {
            LoadingScreenUI.Instance?.Hide();
            return;
        }

        bool loadMenu = scene.name == GameScenes.Boot;
        if (!loadMenu && scene.name != GameScenes.MainMenu)
            return;

        _started = true;
        EnsureHost().StartCoroutine(BootRoutine(loadMenu));
    }

    static IEnumerator BootRoutine(bool loadMenu)
    {
        LoadingScreenUI loadingScreen = LoadingScreenUI.Ensure();
        if (loadingScreen != null)
            loadingScreen.ShowBoot();

        float shownAt = Time.realtimeSinceStartup;

        // Overlay canvas needs one rendered frame; the prefab is saved at scale 0.
        yield return null;
        if (loadingScreen != null)
            loadingScreen.ShowBoot();

        if (loadMenu)
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(GameScenes.MainMenu);
            if (loadOperation != null)
            {
                while (!loadOperation.isDone)
                    yield return null;
            }
        }

        // Floor is 15s so a fast menu still shows the disclaimer. If the menu took longer,
        // hide immediately — do not add another 15s on top of the load.
        while (Time.realtimeSinceStartup - shownAt < MinDisplaySeconds)
            yield return null;

        if (loadingScreen != null)
            loadingScreen.Hide();
    }

    static bool IsDirectGameplayLaunch()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (GameScenes.IsGameScene(SceneManager.GetSceneAt(i)))
                return true;
        }

        return false;
    }

    static Host EnsureHost()
    {
        if (_host != null)
            return _host;

        var go = new GameObject(nameof(GameBootLoader));
        Object.DontDestroyOnLoad(go);
        _host = go.AddComponent<Host>();
        return _host;
    }

    class Host : MonoBehaviour { }
}

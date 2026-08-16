using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads the gameplay scene behind a loading overlay and waits until cards/bootstrap are ready.
/// </summary>
public class GameSceneLoader : MonoBehaviour
{
    static GameSceneLoader _runner;
    static bool _isLoading;

    public static void LoadGame()
    {
        if (_isLoading)
            return;

        EnsureRunner().StartCoroutine(LoadGameRoutine());
    }

    static GameSceneLoader EnsureRunner()
    {
        if (_runner != null)
            return _runner;

        var root = new GameObject(nameof(GameSceneLoader));
        DontDestroyOnLoad(root);
        _runner = root.AddComponent<GameSceneLoader>();
        return _runner;
    }

    static IEnumerator LoadGameRoutine()
    {
        _isLoading = true;

        LoadingScreenUI loadingScreen = LoadingScreenUI.Ensure();
        loadingScreen.Show();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        yield return null;

        while (!GameAssetPrewarm.IsComplete)
            yield return null;

        CardInstancedRenderManager.ResetGameplayReady();

        ThreadPriority previousPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.Low;

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(GameScenes.Game, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            Application.backgroundLoadingPriority = previousPriority;
            Debug.LogError("GameSceneLoader: Failed to start loading " + GameScenes.Game + ".");
            loadingScreen.Hide();
            _isLoading = false;
            yield break;
        }

        loadOperation.allowSceneActivation = false;

        while (loadOperation.progress < 0.9f)
            yield return null;

        loadOperation.allowSceneActivation = true;

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

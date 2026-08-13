using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies shop wall toon shading to exterior house walls when MainScene loads.
/// </summary>
static class ExteriorWallToonBootstrap
{
    const string MainSceneName = "MainScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterSceneLoad()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != MainSceneName)
            return;

        Material wallTemplate = ExteriorWallToonUtility.LoadWallTemplate();
        if (wallTemplate == null)
            return;

        ExteriorWallToonUtility.ApplyAll(wallTemplate, useSharedMaterials: true);
    }
}

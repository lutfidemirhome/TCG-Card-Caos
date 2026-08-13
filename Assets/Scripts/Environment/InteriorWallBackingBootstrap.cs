using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Hides the brick backing submesh on placed House_*_Wall_* meshes when MainScene loads.
/// </summary>
static class InteriorWallBackingBootstrap
{
    const string MainSceneName = "MainScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterSceneLoad()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != MainSceneName)
            return;

        ExteriorWallToonUtility.HideAllPlacedHouseWallBacking(useSharedMaterials: false);
    }
}

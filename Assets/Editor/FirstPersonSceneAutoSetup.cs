#if UNITY_EDITOR
using UnityEditor;

[InitializeOnLoad]
static class FirstPersonSceneAutoSetup
{
    const string ScenePath = "Assets/Scenes/MainScene.unity";

    static FirstPersonSceneAutoSetup()
    {
        EditorApplication.delayCall += TryCreatePlaygroundOnce;
    }

    static void TryCreatePlaygroundOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (System.IO.File.Exists(ScenePath))
            return;

        FirstPersonSceneSetup.SetupPlayground();
    }
}
#endif

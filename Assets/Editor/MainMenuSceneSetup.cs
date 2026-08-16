using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Creates or opens the main menu scene.
/// Menu: TCG Card Caos → Create Main Menu Scene
/// </summary>
public static class MainMenuSceneSetup
{
    const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
    const string GameScenePath = "Assets/Scenes/MainScene.unity";

    [MenuItem("TCG Card Caos/Open Main Menu Scene")]
    public static void OpenMenuScene()
    {
        if (!System.IO.File.Exists(MenuScenePath))
        {
            CreateMenuScene();
            return;
        }

        if (EditorSceneManager.GetActiveScene().path == MenuScenePath)
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
    }

    [MenuItem("TCG Card Caos/Create Main Menu Scene")]
    public static void CreateMenuScene()
    {
        EnsureFolder("Assets/Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var menuRoot = new GameObject("MainMenu");
        menuRoot.AddComponent<MainMenuController>();
        CreateMenuCamera();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, MenuScenePath);
        EnsureBuildSettings();

        Selection.activeGameObject = menuRoot;
        Debug.Log("Main menu scene created at " + MenuScenePath);
    }

    [MenuItem("TCG Card Caos/Configure Build Scenes (Menu + Game)")]
    public static void EnsureBuildSettings()
    {
        if (!System.IO.File.Exists(MenuScenePath))
        {
            Debug.LogWarning("MenuScene not found. Run Create Main Menu Scene first.");
            return;
        }

        if (!System.IO.File.Exists(GameScenePath))
        {
            Debug.LogWarning("MainScene not found.");
            return;
        }

        var scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true)
        };

        EditorBuildSettings.scenes = scenes;
        Debug.Log("Build settings updated: MenuScene first, then MainScene.");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int lastSlash = path.LastIndexOf('/');
        string parent = path.Substring(0, lastSlash);
        string folderName = path.Substring(lastSlash + 1);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    static void CreateMenuCamera()
    {
        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";

        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.18f, 0.42f, 0.78f, 1f);
        camera.depth = -1;

        cameraGo.AddComponent<AudioListener>();
    }
}

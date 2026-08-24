using UnityEngine.SceneManagement;

/// <summary>
/// Scene names used for menu vs gameplay flow.
/// </summary>
public static class GameScenes
{
    public const string Boot = "BootScene";
    public const string MainMenu = "MenuScene";
    public const string Game = "MainScene";

    public static bool IsGameScene(Scene scene) =>
        scene.IsValid() && scene.name == Game;

    public static bool IsActiveGameScene() =>
        IsGameScene(SceneManager.GetActiveScene());
}

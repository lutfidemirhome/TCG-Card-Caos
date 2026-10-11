using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Local Windows test package; never uploads a build or changes Steam achievements.</summary>
public static class SteamAchievementTestBuilder
{
    public const string OutputFolder = "Builds/SteamAchievementTest-Windows";

    [MenuItem("TCG Card Chaos/Steam/Build Windows Achievement Test")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new BuildFailedException("Exit Play mode before building.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            throw new BuildFailedException("Install Windows Build Support (Mono) for this Unity version.");
        if (GameBuildVariant.IsDemo)
            throw new BuildFailedException("Steam achievements must be tested in the full game, not the demo.");
        if (PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone) != ScriptingImplementation.Mono2x)
            throw new BuildFailedException("This Mac-to-Windows test build requires the Mono scripting backend.");
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) throw new BuildFailedException("No scenes enabled for the build.");
        string directory = Path.GetFullPath(OutputFolder);
        Directory.CreateDirectory(directory);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(directory, "TCGCardChaos.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development | BuildOptions.CompressWithLz4,
            extraScriptingDefines = new[] { "TCG_STEAM_TEST" }
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("Steam achievement test build failed: " + report.summary.result);

        // Local testing only: never put this App ID override in a customer depot.
        File.WriteAllText(Path.Combine(directory, "steam_appid.txt"), "5125130\n");
        File.WriteAllText(Path.Combine(directory, "TESTI_BASLAT.cmd"),
            "@echo off\r\ncd /d \"%~dp0\"\r\n"
            + "if exist SteamAchievementTest.log copy /y SteamAchievementTest.log SteamAchievementTest.previous.log >nul\r\n"
            + "\"%~dp0TCGCardChaos.exe\" -logFile \"%~dp0SteamAchievementTest.log\"\r\n");
        File.WriteAllText(Path.Combine(directory, "SONUCU_GOSTER.cmd"),
            "@echo off\r\ncd /d \"%~dp0\"\r\n"
            + "findstr /L /C:\"[Steam Test]\" /C:\"[Steam Skills]\" SteamAchievementTest.log\r\npause\r\n");
        string instructions = "Docs/SteamAchievementWindowsTest.txt";
        if (File.Exists(instructions))
            File.Copy(instructions, Path.Combine(directory, "ONCE_OKU.txt"), true);
        Debug.Log("[Steam Test Build] SUCCESS " + directory + " | " + report.summary.totalSize + " bytes");
        EditorUtility.RevealInFinder(directory);
    }
}

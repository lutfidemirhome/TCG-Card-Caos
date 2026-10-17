using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

// Editor-only recording presets; never changes the game's renderer or build settings.
[InitializeOnLoad]
public static class CpiRecordingSetup
{
    const string Menu = "Tools/TCG Trailer/";
    const string RestoreKey = "TCG.Trailer.RestoreGameView";
    static RecorderController activeRecording;

    static CpiRecordingSetup()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                activeRecording?.StopRecording();
                activeRecording = null;
            }
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(RestoreKey, false))
                return;
            SessionState.SetBool(RestoreKey, false);
            var view = FindGameView();
            if (view != null) view.maximized = false;
        };
        AssemblyReloadEvents.beforeAssemblyReload += () =>
        {
            activeRecording?.StopRecording();
            activeRecording = null;
        };
    }

    [MenuItem(Menu + "1 - 4K 30 FPS - Premiere ProRes HQ")]
    public static void Quality4K() => Configure(3840, 2160, 30, true);

    [MenuItem(Menu + "2 - 1440p 30 FPS - Compact MP4")]
    public static void Smooth1440() => Configure(2560, 1440, 30, false);

    [MenuItem(Menu + "1440p 60 FPS - Higher Recording Load", false, 3)]
    static void HighFrameRate1440() => Configure(2560, 1440, 60, false);

    [MenuItem(Menu + "1 - 4K 30 FPS - Premiere ProRes HQ", true)]
    [MenuItem(Menu + "2 - 1440p 30 FPS - Compact MP4", true)]
    [MenuItem(Menu + "1440p 60 FPS - Higher Recording Load", true)]
    static bool CanConfigure() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

    static void Configure(int width, int height, int fps, bool hq)
    {
        if (!CanConfigure()) return;
        const string prefs = "Library/Recorder/recorder.pref";
        const string backup = "Library/Recorder/recorder.before-cpi.pref";
        if (File.Exists(prefs) && !File.Exists(backup)) File.Copy(prefs, backup);
        var settings = RecorderControllerSettings.GetGlobalSettings();
        foreach (var recorder in settings.RecorderSettings) recorder.Enabled = false;
        var movie = settings.RecorderSettings.OfType<MovieRecorderSettings>().FirstOrDefault();
        if (movie == null)
        {
            movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            settings.AddRecorderSettings(movie);
        }
        movie.name = movie.name.Length == 0 ? "Movie" : movie.name;
        movie.Enabled = true;
        movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = width, OutputHeight = height };
        if (hq)
            movie.EncoderSettings = new ProResEncoderSettings { Format = ProResEncoderSettings.OutputFormat.ProRes422HQ };
        else
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Custom,
                TargetBitRate = fps > 30 ? 40f : 25f
            };
        movie.CaptureAlpha = false;
        movie.CaptureAudio = true;
        movie.AccumulationSettings.CaptureAccumulation = false;
        var outputDirectory = Path.GetFullPath("Recordings/Trailer");
        Directory.CreateDirectory(outputDirectory);
        movie.OutputFile = Path.Combine(outputDirectory, $"TCG_{height}p{fps}_<Take>");
        settings.FrameRatePlayback = FrameRatePlayback.Constant;
        settings.FrameRate = fps;
        settings.CapFrameRate = true;
        settings.ExitPlayMode = false;
        settings.SetRecordModeToManual();
        settings.Save();
        PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, $"Trailer {height}p");
        EditorWindow.GetWindow<RecorderWindow>().SetRecorderControllerSettings(settings);
        Debug.Log($"Trailer: {width}x{height}, {fps} FPS, {(hq ? "ProRes 422 HQ" : "H.264 MP4")}. Output: Recordings/Trailer.");
    }

    [MenuItem(Menu + "3 - Start Recording (Game View Only)")]
    static void Start()
    {
        var recorder = EditorWindow.GetWindow<RecorderWindow>();
        if (recorder.IsRecording()) return;
        var view = FindGameView();
        SessionState.SetBool(RestoreKey, SessionState.GetBool(RestoreKey, false) || (view != null && !view.maximized));
        if (view != null)
        {
            view.maximized = true;
            view.Focus();
        }
        activeRecording = new RecorderController(RecorderControllerSettings.GetGlobalSettings());
        foreach (var movie in activeRecording.Settings.RecorderSettings.OfType<MovieRecorderSettings>())
            movie.OutputFile = Path.Combine(Path.GetFullPath("Recordings/Trailer"),
                $"TCG_{movie.ImageInputSettings.OutputHeight}p_{DateTime.Now:yyyyMMdd_HHmmss_fff}_<Take>");
        activeRecording.PrepareRecording();
        if (!activeRecording.StartRecording()) Stop();
    }

    [MenuItem(Menu + "3 - Start Recording (Game View Only)", true)]
    static bool CanStart() => !EditorApplication.isCompiling && !EditorUtility.scriptCompilationFailed
        && EditorApplication.isPlaying && activeRecording == null;

    [MenuItem(Menu + "4 - Stop Recording")]
    static void Stop()
    {
        activeRecording?.StopRecording();
        activeRecording = null;
        foreach (var recorder in Resources.FindObjectsOfTypeAll<RecorderWindow>())
            if (recorder.IsRecording()) recorder.StopRecording();
        if (SessionState.GetBool(RestoreKey, false))
        {
            SessionState.SetBool(RestoreKey, false);
            var view = FindGameView();
            if (view != null) view.maximized = false;
        }
    }

    static EditorWindow FindGameView() => Resources.FindObjectsOfTypeAll<EditorWindow>()
        .FirstOrDefault(window => window.GetType().Name == "GameView");
}

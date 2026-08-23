using System;
using UnityEngine;

/// <summary>
/// Player Settings values. Stored in PlayerPrefs so they survive builds and language changes.
/// Apply on boot; Settings UI commits a draft through <see cref="Save"/>.
/// </summary>
public static class GameSettings
{
    const string Prefix = "tcg.settings.";
    public const float DefaultFov = 60f;
    public const float MinFov = 50f;
    public const float MaxFov = 90f;
    public const float DefaultSensitivity = 0.5f;
    public const float LookSpeedScale = 4f;

    public enum QualityTier
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    public struct Snapshot
    {
        public GameLanguage language;
        public int width;
        public int height;
        public int refreshHz;
        public bool fullscreen;
        public QualityTier quality;
        public float fov;
        public float sensitivity;
        public bool invertY;
        public bool invertX;
        public float masterVolume;
        public float musicVolume;
        public float sfxVolume;
    }

    static bool _loaded;
    static Snapshot _current;

    public static event Action Changed;

    public static Snapshot Current
    {
        get
        {
            EnsureLoaded();
            return _current;
        }
    }

    public static float LookSensitivity => Current.sensitivity * LookSpeedScale;
    public static bool InvertX => Current.invertX;
    public static bool InvertY => Current.invertY;
    public static float Fov => Current.fov;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyOnBoot()
    {
        EnsureLoaded();
        if (!PlayerPrefs.HasKey(Prefix + "version"))
        {
            AudioListener.volume = _current.masterVolume;
            ApplyCameraFov(_current.fov);
            return;
        }

        Apply(_current, persist: false);
    }

    public static Snapshot CaptureHardwareDefaults()
    {
        EnsureLoaded();
        Resolution current = Screen.currentResolution;
        return new Snapshot
        {
            language = Localization.CurrentLanguage,
            width = Screen.width,
            height = Screen.height,
            refreshHz = Mathf.Max(1, Mathf.RoundToInt((float)current.refreshRateRatio.value)),
            fullscreen = Screen.fullScreen,
            quality = FromQualityLevel(QualitySettings.GetQualityLevel()),
            fov = DefaultFov,
            sensitivity = DefaultSensitivity,
            invertY = false,
            invertX = false,
            masterVolume = 1f,
            musicVolume = GameAudioSettings.MusicVolume,
            sfxVolume = GameAudioSettings.SfxVolume
        };
    }

    public static void Save(Snapshot snapshot)
    {
        _current = Sanitize(snapshot);
        WritePrefs(_current);
        Apply(_current, persist: true);
        Changed?.Invoke();
    }

    public static void ApplyLookAndAudio(Snapshot snapshot)
    {
        snapshot = Sanitize(snapshot);
        AudioListener.volume = snapshot.masterVolume;
        GameAudioSettings.SetMusicVolume(snapshot.musicVolume);
        GameAudioSettings.SetSfxVolume(snapshot.sfxVolume);
        ApplyCameraFov(snapshot.fov);
    }

    public static void ApplyLanguage(GameLanguage language)
    {
        Localization.SetLanguage(language);
    }

    public static void ApplyCameraFov(float fov)
    {
        fov = Mathf.Clamp(fov, MinFov, MaxFov);
        FirstPersonController player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
        if (player != null)
            player.ApplySettingsFov(fov);
        else if (Camera.main != null)
            Camera.main.fieldOfView = fov;
    }

    static void Apply(Snapshot snapshot, bool persist)
    {
        Localization.SetLanguage(snapshot.language);
        ApplyQualityIfNeeded(snapshot.quality);
        ApplyDisplayIfNeeded(snapshot);
        AudioListener.volume = snapshot.masterVolume;
        GameAudioSettings.SetMusicVolume(snapshot.musicVolume);
        GameAudioSettings.SetSfxVolume(snapshot.sfxVolume);
        ApplyCameraFov(snapshot.fov);
        if (persist)
            WritePrefs(snapshot);
    }

    /// <summary>
    /// Language / audio / look saves must not reload quality. <c>applyExpensiveChanges</c> rebuilds
    /// shaders and mipmaps and is what made UI art look like placeholders and distant cards blur.
    /// </summary>
    static void ApplyQualityIfNeeded(QualityTier quality)
    {
        int level = ToQualityLevel(quality);
        if (QualitySettings.GetQualityLevel() == level)
            return;

        QualitySettings.SetQualityLevel(level, applyExpensiveChanges: true);
    }

    /// <summary>
    /// <see cref="Screen.SetResolution"/> on an unchanged mode still tears down the swapchain and
    /// rebuilds every canvas — that is what broke Settings after a language-only Save.
    /// </summary>
    static void ApplyDisplayIfNeeded(Snapshot snapshot)
    {
        bool sizeChanged = Screen.width != snapshot.width || Screen.height != snapshot.height;
        bool fullscreenChanged = Screen.fullScreen != snapshot.fullscreen;
        if (!sizeChanged && !fullscreenChanged)
            return;

        Screen.SetResolution(snapshot.width, snapshot.height, snapshot.fullscreen);
    }

    static void EnsureLoaded()
    {
        if (_loaded)
            return;

        _loaded = true;
        Snapshot hardware = new Snapshot
        {
            language = Localization.CurrentLanguage,
            width = Screen.width,
            height = Screen.height,
            refreshHz = Mathf.Max(1, Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value)),
            fullscreen = Screen.fullScreen,
            quality = FromQualityLevel(QualitySettings.GetQualityLevel()),
            fov = DefaultFov,
            sensitivity = DefaultSensitivity,
            invertY = false,
            invertX = false,
            masterVolume = 1f,
            musicVolume = GameAudioSettings.MusicVolume,
            sfxVolume = GameAudioSettings.SfxVolume
        };

        if (!PlayerPrefs.HasKey(Prefix + "version"))
        {
            _current = hardware;
            return;
        }

        _current = Sanitize(new Snapshot
        {
            language = (GameLanguage)PlayerPrefs.GetInt(Prefix + "language", (int)hardware.language),
            width = PlayerPrefs.GetInt(Prefix + "width", hardware.width),
            height = PlayerPrefs.GetInt(Prefix + "height", hardware.height),
            refreshHz = PlayerPrefs.GetInt(Prefix + "refresh", hardware.refreshHz),
            fullscreen = PlayerPrefs.GetInt(Prefix + "fullscreen", hardware.fullscreen ? 1 : 0) == 1,
            quality = (QualityTier)PlayerPrefs.GetInt(Prefix + "quality", (int)hardware.quality),
            fov = PlayerPrefs.GetFloat(Prefix + "fov", hardware.fov),
            sensitivity = PlayerPrefs.GetFloat(Prefix + "sensitivity", hardware.sensitivity),
            invertY = PlayerPrefs.GetInt(Prefix + "invertY", 0) == 1,
            invertX = PlayerPrefs.GetInt(Prefix + "invertX", 0) == 1,
            masterVolume = PlayerPrefs.GetFloat(Prefix + "master", hardware.masterVolume),
            musicVolume = PlayerPrefs.GetFloat(Prefix + "music", hardware.musicVolume),
            sfxVolume = PlayerPrefs.GetFloat(Prefix + "sfx", hardware.sfxVolume)
        });
    }

    static void WritePrefs(Snapshot snapshot)
    {
        PlayerPrefs.SetInt(Prefix + "version", 1);
        PlayerPrefs.SetInt(Prefix + "language", (int)snapshot.language);
        PlayerPrefs.SetInt(Prefix + "width", snapshot.width);
        PlayerPrefs.SetInt(Prefix + "height", snapshot.height);
        PlayerPrefs.SetInt(Prefix + "refresh", snapshot.refreshHz);
        PlayerPrefs.SetInt(Prefix + "fullscreen", snapshot.fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "quality", (int)snapshot.quality);
        PlayerPrefs.SetFloat(Prefix + "fov", snapshot.fov);
        PlayerPrefs.SetFloat(Prefix + "sensitivity", snapshot.sensitivity);
        PlayerPrefs.SetInt(Prefix + "invertY", snapshot.invertY ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "invertX", snapshot.invertX ? 1 : 0);
        PlayerPrefs.SetFloat(Prefix + "master", snapshot.masterVolume);
        PlayerPrefs.SetFloat(Prefix + "music", snapshot.musicVolume);
        PlayerPrefs.SetFloat(Prefix + "sfx", snapshot.sfxVolume);
        PlayerPrefs.Save();
    }

    static Snapshot Sanitize(Snapshot snapshot)
    {
        snapshot.width = Mathf.Max(640, snapshot.width);
        snapshot.height = Mathf.Max(360, snapshot.height);
        snapshot.refreshHz = Mathf.Clamp(snapshot.refreshHz, 30, 360);
        snapshot.fov = Mathf.Clamp(snapshot.fov, MinFov, MaxFov);
        snapshot.sensitivity = Mathf.Clamp01(snapshot.sensitivity);
        snapshot.masterVolume = Mathf.Clamp01(snapshot.masterVolume);
        snapshot.musicVolume = Mathf.Clamp01(snapshot.musicVolume);
        snapshot.sfxVolume = Mathf.Clamp01(snapshot.sfxVolume);
        if ((int)snapshot.quality < 0 || (int)snapshot.quality > 2)
            snapshot.quality = QualityTier.Medium;
        return snapshot;
    }

    public static int ToQualityLevel(QualityTier tier)
    {
        switch (tier)
        {
            case QualityTier.Low: return 1;
            case QualityTier.High: return 3;
            default: return 2;
        }
    }

    public static QualityTier FromQualityLevel(int level)
    {
        if (level <= 1)
            return QualityTier.Low;
        if (level == 2)
            return QualityTier.Medium;
        return QualityTier.High;
    }

    public static string FormatResolution(int width, int height, int refreshHz)
    {
        return width + "x" + height + "@" + refreshHz + "Hz";
    }
}

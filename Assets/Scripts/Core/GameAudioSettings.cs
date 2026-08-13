using System;

/// <summary>
/// Runtime audio preferences. Settings UI will call the setters later; defaults keep music on.
/// </summary>
public static class GameAudioSettings
{
    public const string MusicEnabledPlayerPrefsKey = "TCGCardCaos.MusicEnabled";
    public const string MusicVolumePlayerPrefsKey = "TCGCardCaos.MusicVolume";
    public const string SfxEnabledPlayerPrefsKey = "TCGCardCaos.SfxEnabled";
    public const string SfxVolumePlayerPrefsKey = "TCGCardCaos.SfxVolume";

    public const float DefaultMusicVolume = 0.6f;
    public const float DefaultSfxVolume = 1f;

    static bool _musicEnabled = true;
    static float _musicVolume = DefaultMusicVolume;
    static bool _sfxEnabled = true;
    static float _sfxVolume = DefaultSfxVolume;
    static bool _loaded;

    public static bool MusicEnabled
    {
        get
        {
            EnsureLoaded();
            return _musicEnabled;
        }
    }

    public static float MusicVolume
    {
        get
        {
            EnsureLoaded();
            return _musicVolume;
        }
    }

    public static bool SfxEnabled
    {
        get
        {
            EnsureLoaded();
            return _sfxEnabled;
        }
    }

    public static float SfxVolume
    {
        get
        {
            EnsureLoaded();
            return _sfxVolume;
        }
    }

    public static event Action Changed;

    public static void SetMusicEnabled(bool enabled)
    {
        EnsureLoaded();
        if (_musicEnabled == enabled)
            return;

        _musicEnabled = enabled;
        Save();
        Changed?.Invoke();
    }

    public static void SetMusicVolume(float volume)
    {
        EnsureLoaded();
        volume = UnityEngine.Mathf.Clamp01(volume);
        if (UnityEngine.Mathf.Approximately(_musicVolume, volume))
            return;

        _musicVolume = volume;
        Save();
        Changed?.Invoke();
    }

    public static void SetSfxEnabled(bool enabled)
    {
        EnsureLoaded();
        if (_sfxEnabled == enabled)
            return;

        _sfxEnabled = enabled;
        Save();
        Changed?.Invoke();
    }

    public static void SetSfxVolume(float volume)
    {
        EnsureLoaded();
        volume = UnityEngine.Mathf.Clamp01(volume);
        if (UnityEngine.Mathf.Approximately(_sfxVolume, volume))
            return;

        _sfxVolume = volume;
        Save();
        Changed?.Invoke();
    }

    static void EnsureLoaded()
    {
        if (_loaded)
            return;

        _loaded = true;
        _musicEnabled = UnityEngine.PlayerPrefs.GetInt(MusicEnabledPlayerPrefsKey, 1) == 1;
        _musicVolume = UnityEngine.Mathf.Clamp01(
            UnityEngine.PlayerPrefs.GetFloat(MusicVolumePlayerPrefsKey, DefaultMusicVolume));
        _sfxEnabled = UnityEngine.PlayerPrefs.GetInt(SfxEnabledPlayerPrefsKey, 1) == 1;
        _sfxVolume = UnityEngine.Mathf.Clamp01(
            UnityEngine.PlayerPrefs.GetFloat(SfxVolumePlayerPrefsKey, DefaultSfxVolume));
    }

    static void Save()
    {
        UnityEngine.PlayerPrefs.SetInt(MusicEnabledPlayerPrefsKey, _musicEnabled ? 1 : 0);
        UnityEngine.PlayerPrefs.SetFloat(MusicVolumePlayerPrefsKey, _musicVolume);
        UnityEngine.PlayerPrefs.SetInt(SfxEnabledPlayerPrefsKey, _sfxEnabled ? 1 : 0);
        UnityEngine.PlayerPrefs.SetFloat(SfxVolumePlayerPrefsKey, _sfxVolume);
        UnityEngine.PlayerPrefs.Save();
    }
}

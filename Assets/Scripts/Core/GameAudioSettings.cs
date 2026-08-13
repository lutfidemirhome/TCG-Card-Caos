using System;

/// <summary>
/// Runtime audio preferences. Settings UI will call the setters later; defaults keep music on.
/// </summary>
public static class GameAudioSettings
{
    public const string MusicEnabledPlayerPrefsKey = "TCGCardCaos.MusicEnabled";
    public const string MusicVolumePlayerPrefsKey = "TCGCardCaos.MusicVolume";
    public const float DefaultMusicVolume = 0.6f;

    static bool _musicEnabled = true;
    static float _musicVolume = DefaultMusicVolume;
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

    static void EnsureLoaded()
    {
        if (_loaded)
            return;

        _loaded = true;
        _musicEnabled = UnityEngine.PlayerPrefs.GetInt(MusicEnabledPlayerPrefsKey, 1) == 1;
        _musicVolume = UnityEngine.Mathf.Clamp01(
            UnityEngine.PlayerPrefs.GetFloat(MusicVolumePlayerPrefsKey, DefaultMusicVolume));
    }

    static void Save()
    {
        UnityEngine.PlayerPrefs.SetInt(MusicEnabledPlayerPrefsKey, _musicEnabled ? 1 : 0);
        UnityEngine.PlayerPrefs.SetFloat(MusicVolumePlayerPrefsKey, _musicVolume);
        UnityEngine.PlayerPrefs.Save();
    }
}

using UnityEngine;

/// <summary>
/// Looped in-game music from Resources/Audio/game_music_ingame/1-11.
/// </summary>
public class BackgroundMusic : MonoBehaviour
{
    public const string ClipResourcePath = "Audio/game_music_ingame/1-11";

    static BackgroundMusic _instance;

    AudioSource _source;
    bool _warnedMissingClip;

    public static BackgroundMusic EnsureExists()
    {
        if (_instance != null)
            return _instance;

        BackgroundMusic existing = FindFirstObjectByType<BackgroundMusic>();
        if (existing != null)
        {
            _instance = existing;
            return existing;
        }

        var root = new GameObject(nameof(BackgroundMusic));
        return root.AddComponent<BackgroundMusic>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        _source = gameObject.AddComponent<AudioSource>();
        _source.loop = true;
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        _source.priority = 0;

        GameAudioSettings.Changed += ApplySettings;
    }

    void OnDestroy()
    {
        GameAudioSettings.Changed -= ApplySettings;

        if (_instance == this)
            _instance = null;
    }

    void Start()
    {
        if (!Application.isPlaying)
            return;

        AudioClip clip = Resources.Load<AudioClip>(ClipResourcePath);
        if (clip == null)
        {
            WarnMissingClipOnce();
            return;
        }

        _source.clip = clip;
        ApplySettings();
    }

    void ApplySettings()
    {
        if (_source == null)
            return;

        _source.volume = GameAudioSettings.MusicVolume;

        if (_source.clip == null)
            return;

        if (GameAudioSettings.MusicEnabled && !_source.isPlaying)
            _source.Play();
        else if (!GameAudioSettings.MusicEnabled && _source.isPlaying)
            _source.Stop();
    }

    void WarnMissingClipOnce()
    {
        if (_warnedMissingClip)
            return;

        _warnedMissingClip = true;
        Debug.LogWarning(
            "BackgroundMusic: No clip at Resources/"
            + ClipResourcePath
            + ". Add 1-11.mp3 under Assets/Resources/Audio/game_music_ingame/.");
    }
}

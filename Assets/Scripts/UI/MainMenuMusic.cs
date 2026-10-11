using UnityEngine;

/// <summary>Scene-local menu music, controlled by the existing music preferences.</summary>
[DisallowMultipleComponent]
public sealed class MainMenuMusic : MonoBehaviour
{
    AudioSource _source;

    void Awake()
    {
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = true;
        _source.spatialBlend = 0f;
        _source.clip = Resources.Load<AudioClip>("Audio/ana-menu-bg-music");
    }

    void OnEnable()
    {
        GameAudioSettings.Changed += ApplySettings;
        ApplySettings();
    }

    void OnDisable()
    {
        GameAudioSettings.Changed -= ApplySettings;
        if (_source != null) _source.Stop();
    }

    void ApplySettings()
    {
        if (_source == null || _source.clip == null) return;
        _source.volume = GameAudioSettings.MusicVolume;
        if (GameAudioSettings.MusicEnabled)
        {
            if (!_source.isPlaying) _source.Play();
        }
        else if (_source.isPlaying) _source.Stop();
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sequential in-game playlist: Resources/Audio/1 … 5, then back to 1.
/// </summary>
public class BackgroundMusic : MonoBehaviour
{
    public const string ClipResourceFolder = "Audio/game_music_ingame";
    public const int TrackCount = 5;

    static BackgroundMusic _instance;

    readonly List<AudioClip> _tracks = new List<AudioClip>(TrackCount);
    AudioSource _source;
    Coroutine _playlistRoutine;
    int _currentTrackIndex;
    bool _warnedMissingTracks;

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
        _source.loop = false;
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

        LoadTracks();
        ApplySettings();
    }

    void LoadTracks()
    {
        _tracks.Clear();

        for (int i = 1; i <= TrackCount; i++)
        {
            AudioClip clip = Resources.Load<AudioClip>(ClipResourceFolder + "/" + i);
            if (clip != null)
                _tracks.Add(clip);
        }

        if (_tracks.Count == 0)
            WarnMissingTracksOnce();
    }

    void ApplySettings()
    {
        if (_source == null)
            return;

        _source.volume = GameAudioSettings.MusicVolume;

        if (_tracks.Count == 0)
            return;

        if (GameAudioSettings.MusicEnabled)
            StartPlaylistIfNeeded();
        else
            StopPlaylist();
    }

    void StartPlaylistIfNeeded()
    {
        if (_playlistRoutine != null)
            return;

        _playlistRoutine = StartCoroutine(PlayPlaylistLoop());
    }

    void StopPlaylist()
    {
        if (_playlistRoutine != null)
        {
            StopCoroutine(_playlistRoutine);
            _playlistRoutine = null;
        }

        if (_source.isPlaying)
            _source.Stop();
    }

    IEnumerator PlayPlaylistLoop()
    {
        while (true)
        {
            if (!GameAudioSettings.MusicEnabled || _tracks.Count == 0)
            {
                _playlistRoutine = null;
                yield break;
            }

            _currentTrackIndex = Mathf.Clamp(_currentTrackIndex, 0, _tracks.Count - 1);
            AudioClip clip = _tracks[_currentTrackIndex];
            _source.clip = clip;
            _source.volume = GameAudioSettings.MusicVolume;
            _source.Play();

            yield return new WaitWhile(() => _source.isPlaying);

            _currentTrackIndex = (_currentTrackIndex + 1) % _tracks.Count;
        }
    }

    void WarnMissingTracksOnce()
    {
        if (_warnedMissingTracks)
            return;

        _warnedMissingTracks = true;
        Debug.LogWarning(
            "BackgroundMusic: No tracks found. Add 1.wav … "
            + TrackCount
            + ".wav under Assets/Resources/Audio/game_music_ingame/ (names: 1, 2, 3, …).");
    }
}

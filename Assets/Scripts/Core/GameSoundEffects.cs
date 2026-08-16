using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One-shot gameplay SFX loaded from Resources/Audio/.
/// </summary>
public class GameSoundEffects : MonoBehaviour
{
    public const string ResourceFolder = "Audio/sfx";
    public const string PackSoundsFolder = "Audio/Card pack sounds";

    public static class Id
    {
        public const string CardPickup = "card_pickup";
        public const string CardShelfPlace = "card_shelf_place";
        public const string CardThrow = "card_throw";
        public const string CardHandScroll = "card_hand_scroll";
    }

    public static class PackId
    {
        public const string PackOpen = "pack_open";
        public const string CardRotation = "card_rotation";
        public const string InCardLayout = "in_card_layout";
        public const string WhileGathering = "while_gathering";
    }

    public const float CardHandScrollVolume = 0.6f;

    const int AudioSourcePoolSize = 8;

    static GameSoundEffects _instance;

    readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>(8);
    readonly HashSet<string> _warnedMissing = new HashSet<string>();
    AudioSource[] _sources;
    int _nextSourceIndex;

    public static GameSoundEffects EnsureExists()
    {
        if (_instance != null)
            return _instance;

        GameSoundEffects existing = FindFirstObjectByType<GameSoundEffects>();
        if (existing != null)
        {
            _instance = existing;
            return existing;
        }

        var root = new GameObject(nameof(GameSoundEffects));
        return root.AddComponent<GameSoundEffects>();
    }

    public static void Play(string effectId, float volumeScale = 1f)
    {
        if (string.IsNullOrWhiteSpace(effectId))
            return;

        EnsureExists();
        _instance.PlayInternal(ResourceFolder, effectId, volumeScale);
    }

    public static void PlayPack(string clipName, float volumeScale = 1f)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return;

        EnsureExists();
        _instance.PlayInternal(PackSoundsFolder, clipName, volumeScale);
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        _sources = new AudioSource[AudioSourcePoolSize];

        for (int i = 0; i < AudioSourcePoolSize; i++)
        {
            _sources[i] = gameObject.AddComponent<AudioSource>();
            _sources[i].playOnAwake = false;
            _sources[i].loop = false;
            _sources[i].spatialBlend = 0f;
            _sources[i].priority = 128;
        }
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void PlayInternal(string resourceFolder, string clipName, float volumeScale)
    {
        if (!GameAudioSettings.SfxEnabled)
            return;

        AudioClip clip = LoadClip(resourceFolder, clipName);
        if (clip == null)
            return;

        AudioSource source = GetNextSource();
        float volume = GameAudioSettings.SfxVolume * Mathf.Clamp01(volumeScale);
        source.PlayOneShot(clip, volume);
    }

    AudioSource GetNextSource()
    {
        AudioSource source = _sources[_nextSourceIndex];
        _nextSourceIndex = (_nextSourceIndex + 1) % _sources.Length;
        return source;
    }

    AudioClip LoadClip(string resourceFolder, string clipName)
    {
        string cacheKey = resourceFolder + "/" + clipName;
        if (_clips.TryGetValue(cacheKey, out AudioClip cached))
            return cached;

        AudioClip clip = Resources.Load<AudioClip>(cacheKey);
        _clips[cacheKey] = clip;

        if (clip == null)
            WarnMissingOnce(cacheKey);

        return clip;
    }

    void WarnMissingOnce(string resourcePath)
    {
        if (!_warnedMissing.Add(resourcePath))
            return;

        Debug.LogWarning(
            "GameSoundEffects: Missing clip Resources/"
            + resourcePath
            + " (.wav/.mp3/.ogg).");
    }
}

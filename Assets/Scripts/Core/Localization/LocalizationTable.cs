using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Translation source for every UI string. One entry per <see cref="LocalizationKeys"/> constant,
/// with one value per <see cref="GameLanguage"/> (indexed by the enum value).
/// Lives in Resources so <see cref="Localization"/> can load it without a scene reference.
/// </summary>
[CreateAssetMenu(menuName = "TCG Card Caos/Localization Table", fileName = LocalizationTable.AssetName)]
public class LocalizationTable : ScriptableObject
{
    public const string AssetName = "LocalizationTable";
    public const string ResourcePath = "Localization/" + AssetName;

    [Serializable]
    public class Entry
    {
        public string key;

        [Tooltip("One value per GameLanguage, in enum order. Empty entries fall back to English.")]
        [TextArea(1, 8)]
        public string[] values = new string[GameLanguages.Count];
    }

    [SerializeField] List<Entry> entries = new List<Entry>();

    Dictionary<string, Entry> _lookup;

    public IReadOnlyList<Entry> Entries => entries;

    /// <summary>
    /// Returns the translated value, falling back to English and then to the key itself so a
    /// missing translation is visible in-game instead of rendering as empty space.
    /// </summary>
    public string Get(string key, GameLanguage language)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        EnsureLookup();
        if (!_lookup.TryGetValue(key, out Entry entry) || entry.values == null)
            return key;

        int index = (int)language;
        if (index >= 0 && index < entry.values.Length && !string.IsNullOrEmpty(entry.values[index]))
            return entry.values[index];

        int english = (int)GameLanguage.English;
        if (english < entry.values.Length && !string.IsNullOrEmpty(entry.values[english]))
            return entry.values[english];

        return key;
    }

    public bool HasKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        EnsureLookup();
        return _lookup.ContainsKey(key);
    }

    void EnsureLookup()
    {
        if (_lookup != null)
            return;

        _lookup = new Dictionary<string, Entry>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry == null || string.IsNullOrEmpty(entry.key))
                continue;

            _lookup[entry.key] = entry;
        }
    }

    void OnEnable() => _lookup = null;

#if UNITY_EDITOR
    /// <summary>Adds the key if missing and resizes value arrays to the current language count.</summary>
    public void EditorEnsureKey(string key, string english = null, string turkish = null)
    {
        EnsureRowSizes();

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].key == key)
                return;
        }

        var entry = new Entry { key = key, values = new string[GameLanguages.Count] };
        if (english != null)
            entry.values[(int)GameLanguage.English] = english;
        if (turkish != null)
            entry.values[(int)GameLanguage.Turkish] = turkish;

        entries.Add(entry);
        _lookup = null;
    }

    public void EditorEnsureRowSizes() => EnsureRowSizes();

    void EnsureRowSizes()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry == null)
                continue;

            if (entry.values == null)
            {
                entry.values = new string[GameLanguages.Count];
                continue;
            }

            if (entry.values.Length != GameLanguages.Count)
                Array.Resize(ref entry.values, GameLanguages.Count);
        }

        _lookup = null;
    }
#endif
}

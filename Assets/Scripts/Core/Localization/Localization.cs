using System;
using UnityEngine;

/// <summary>
/// Runtime entry point for translated strings. The selected language is stored in PlayerPrefs and
/// defaults to the player's OS language on first launch.
/// </summary>
public static class Localization
{
    const string LanguagePrefsKey = "tcg.language";

    static LocalizationTable _table;
    static GameLanguage _currentLanguage;
    static bool _initialized;

    /// <summary>Raised after <see cref="SetLanguage"/> changes the language so bound texts refresh.</summary>
    public static event Action LanguageChanged;

    public static GameLanguage CurrentLanguage
    {
        get
        {
            EnsureInitialized();
            return _currentLanguage;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _table = null;
        _initialized = false;
        LanguageChanged = null;
    }

    public static string Get(string key)
    {
        EnsureInitialized();
        return _table != null ? _table.Get(key, _currentLanguage) : key;
    }

    public static void ReloadTable()
    {
        _table = null;
        _initialized = false;
        EnsureInitialized();
    }

    public static string Format(string key, params object[] args)
    {
        string value = Get(key);
        if (args == null || args.Length == 0)
            return value;

        try
        {
            return string.Format(value, args);
        }
        catch (FormatException)
        {
            return value;
        }
    }

    public static void SetLanguage(GameLanguage language)
    {
        EnsureInitialized();
        if (_currentLanguage == language)
            return;

        _currentLanguage = language;
        PlayerPrefs.SetInt(LanguagePrefsKey, (int)language);
        PlayerPrefs.Save();

        LanguageChanged?.Invoke();
    }

    /// <summary>Editor/preview helper: applies a language without writing to PlayerPrefs.</summary>
    public static void SetLanguagePreview(GameLanguage language)
    {
        EnsureInitialized();
        if (_currentLanguage == language)
            return;

        _currentLanguage = language;
        LanguageChanged?.Invoke();
    }

    static void EnsureInitialized()
    {
        if (_initialized)
            return;

        _initialized = true;
        _table = Resources.Load<LocalizationTable>(LocalizationTable.ResourcePath);

        if (_table == null)
        {
            Debug.LogWarning(
                "Localization: no table at Resources/" + LocalizationTable.ResourcePath
                + ". Run TCG Card Chaos → UI → Build Main Menu UI to create it.");
        }

        _currentLanguage = PlayerPrefs.HasKey(LanguagePrefsKey)
            ? (GameLanguage)PlayerPrefs.GetInt(LanguagePrefsKey)
            : GameLanguages.FromSystemLanguage(Application.systemLanguage);
    }
}

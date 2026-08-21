using System.Globalization;

/// <summary>
/// The 12 shipping languages. Enum order defines the column order in <see cref="LocalizationTable"/>,
/// so never reorder or remove entries once translations exist — only append.
/// </summary>
public enum GameLanguage
{
    English = 0,
    Turkish = 1,
    French = 2,
    Italian = 3,
    German = 4,
    SpanishCastilian = 5,
    PortugueseBrazil = 6,
    ChineseSimplified = 7,
    ChineseTraditional = 8,
    Japanese = 9,
    Korean = 10,
    Russian = 11,
}

/// <summary>
/// Writing system a language needs. Baloo 2 only ships Latin glyphs, so every group other than
/// <see cref="ScriptGroup.Latin"/> needs a TMP fallback font asset covering that range.
/// </summary>
public enum ScriptGroup
{
    Latin,
    Cyrillic,
    ChineseSimplified,
    ChineseTraditional,
    Japanese,
    Korean,
}

public static class GameLanguages
{
    public const int Count = 12;

    /// <summary>Name shown in the language picker, written in the language itself.</summary>
    public static string GetNativeName(GameLanguage language) => language switch
    {
        GameLanguage.English => "English",
        GameLanguage.Turkish => "Türkçe",
        GameLanguage.French => "Français",
        GameLanguage.Italian => "Italiano",
        GameLanguage.German => "Deutsch",
        GameLanguage.SpanishCastilian => "Español (España)",
        GameLanguage.PortugueseBrazil => "Português (Brasil)",
        GameLanguage.ChineseSimplified => "简体中文",
        GameLanguage.ChineseTraditional => "繁體中文",
        GameLanguage.Japanese => "日本語",
        GameLanguage.Korean => "한국어",
        GameLanguage.Russian => "Русский",
        _ => language.ToString(),
    };

    /// <summary>Culture code used for translator hand-off and store metadata.</summary>
    public static string GetCultureCode(GameLanguage language) => language switch
    {
        GameLanguage.English => "en",
        GameLanguage.Turkish => "tr",
        GameLanguage.French => "fr",
        GameLanguage.Italian => "it",
        GameLanguage.German => "de",
        GameLanguage.SpanishCastilian => "es-ES",
        GameLanguage.PortugueseBrazil => "pt-BR",
        GameLanguage.ChineseSimplified => "zh-Hans",
        GameLanguage.ChineseTraditional => "zh-Hant",
        GameLanguage.Japanese => "ja",
        GameLanguage.Korean => "ko",
        GameLanguage.Russian => "ru",
        _ => "en",
    };

    public static ScriptGroup GetScriptGroup(GameLanguage language) => language switch
    {
        GameLanguage.ChineseSimplified => ScriptGroup.ChineseSimplified,
        GameLanguage.ChineseTraditional => ScriptGroup.ChineseTraditional,
        GameLanguage.Japanese => ScriptGroup.Japanese,
        GameLanguage.Korean => ScriptGroup.Korean,
        GameLanguage.Russian => ScriptGroup.Cyrillic,
        _ => ScriptGroup.Latin,
    };

    /// <summary>
    /// Uppercases copy using the active language's casing rules (e.g. Turkish i → İ, not I).
    /// </summary>
    public static string ToUpper(string text, GameLanguage language)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        try
        {
            return text.ToUpper(CultureInfo.GetCultureInfo(GetCultureCode(language)));
        }
        catch (CultureNotFoundException)
        {
            return text.ToUpperInvariant();
        }
    }

    /// <summary>Maps the player's OS language onto a shipping language, defaulting to English.</summary>
    public static GameLanguage FromSystemLanguage(UnityEngine.SystemLanguage systemLanguage) => systemLanguage switch
    {
        UnityEngine.SystemLanguage.Turkish => GameLanguage.Turkish,
        UnityEngine.SystemLanguage.French => GameLanguage.French,
        UnityEngine.SystemLanguage.Italian => GameLanguage.Italian,
        UnityEngine.SystemLanguage.German => GameLanguage.German,
        UnityEngine.SystemLanguage.Spanish => GameLanguage.SpanishCastilian,
        UnityEngine.SystemLanguage.Portuguese => GameLanguage.PortugueseBrazil,
        UnityEngine.SystemLanguage.ChineseSimplified => GameLanguage.ChineseSimplified,
        UnityEngine.SystemLanguage.ChineseTraditional => GameLanguage.ChineseTraditional,
        UnityEngine.SystemLanguage.Chinese => GameLanguage.ChineseSimplified,
        UnityEngine.SystemLanguage.Japanese => GameLanguage.Japanese,
        UnityEngine.SystemLanguage.Korean => GameLanguage.Korean,
        UnityEngine.SystemLanguage.Russian => GameLanguage.Russian,
        _ => GameLanguage.English,
    };
}

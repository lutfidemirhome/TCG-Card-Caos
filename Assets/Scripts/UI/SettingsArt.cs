using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings slices in Assets/UI/Settings/Art, plus reused Load Game / pause art.
/// </summary>
public static class SettingsArt
{
    const string SettingsFolder = "Assets/UI/Settings/Art/";
    const string LoadFolder = "Assets/UI/LoadGame/Art/";
    const string PauseFolder = "Assets/UI/ingame/";

    public static Sprite Background => Load(LoadFolder, "UI/LoadGame/", "load_game_bg.png");
    public static Sprite Panel => Load(LoadFolder, "UI/LoadGame/", "panel_1.png");
    public static Sprite LanguageDropdown => Load(SettingsFolder, "UI/Settings/", "language_button.png");
    public static Sprite ResolutionDropdown => Load(SettingsFolder, "UI/Settings/", "resolution_button.png");
    public static Sprite QualityDropdown => Load(SettingsFolder, "UI/Settings/", "medium_button.png");
    public static Sprite CheckboxOff => Load(SettingsFolder, "UI/Settings/", "check_bg.png");
    public static Sprite CheckboxOn => Load(SettingsFolder, "UI/Settings/", "approval_icon.png");
    public static Sprite SliderTrack => Load(SettingsFolder, "UI/Settings/", "settings_bar_bg.png");
    public static Sprite SliderFill => Load(SettingsFolder, "UI/Settings/", "settings_bar.png");
    public static Sprite SliderHandle => Load(SettingsFolder, "UI/Settings/", "bar_circle.png");
    public static Sprite ValueBox => CheckboxOff;
    public static Sprite ButtonSave => Load(LoadFolder, "UI/LoadGame/", "yes_button.png");
    public static Sprite EscIcon => Load(PauseFolder, "UI/ingame/", "esc_icon.png");

    public static void Apply(Image image, Sprite sprite, Color fallback)
    {
        if (image == null)
            return;

        if (sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
            image.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = sprite.border.sqrMagnitude <= 0f;
            return;
        }

        image.sprite = null;
        image.color = fallback;
    }

    static Sprite Load(string artFolder, string resourceFolder, string fileName)
    {
#if UNITY_EDITOR
        string path = artFolder + fileName;
        Sprite fromArt = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (fromArt != null)
            return fromArt;

        UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
                return sprite;
        }
#endif
        return Resources.Load<Sprite>(resourceFolder + Path.GetFileNameWithoutExtension(fileName));
    }
}

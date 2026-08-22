using UnityEngine;

/// <summary>
/// Sprites for the Load Game confirm overlay (Resources/UI/LoadGame).
/// </summary>
public static class LoadGameConfirmArt
{
    const string ResourceRoot = "UI/LoadGame/";

    public static Sprite Band => Load("confirm_band");
    public static Sprite YesButton => Load("yes_button");
    public static Sprite NoButton => Load("no_button");

    static Sprite Load(string assetName)
    {
        if (string.IsNullOrEmpty(assetName))
            return null;

        return Resources.Load<Sprite>(ResourceRoot + assetName);
    }
}

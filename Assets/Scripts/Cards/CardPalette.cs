using UnityEngine;

/// <summary>
/// Shared face colors for prototype cards. Later each card id maps to art/atlas UVs.
/// </summary>
public static class CardPalette
{
    public const int Count = 8;

    static readonly Color[] Colors =
    {
        new Color(0.25f, 0.55f, 0.95f),
        new Color(0.95f, 0.35f, 0.35f),
        new Color(0.35f, 0.85f, 0.45f),
        new Color(0.85f, 0.65f, 0.2f),
        new Color(0.65f, 0.4f, 0.95f),
        new Color(0.95f, 0.55f, 0.15f),
        new Color(0.2f, 0.75f, 0.8f),
        new Color(0.8f, 0.3f, 0.55f),
    };

    public static Color GetColor(int paletteIndex)
    {
        if (Colors.Length == 0)
            return Color.white;

        int index = paletteIndex % Colors.Length;
        if (index < 0)
            index += Colors.Length;

        return Colors[index];
    }
}

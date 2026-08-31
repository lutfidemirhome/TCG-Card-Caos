/// <summary>
/// Fallback HUD denominators when live counters are unavailable.
/// Full builds use <see cref="GameProgressCounter"/> (98 cabinets, 5151 cards).
/// </summary>
public static class GameHudLimits
{
    public const int MaxShelves = 6;
    public const int MaxPlacedCards = 264;

    public const int FullMaxCabinets = 98;
    public const int FullMaxPlacedCards = 5151;
}

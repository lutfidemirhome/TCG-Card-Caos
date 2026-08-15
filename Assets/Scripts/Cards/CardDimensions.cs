public static class CardDimensions
{
    public const int MaxHandSize = 10;

    /// <summary>Cards revealed when opening one booster pack.</summary>
    public const int CardsPerBoosterPack = 5;

    /// <summary>Max cards in hand while holding a pack (pack opening needs five free slots).</summary>
    public const int MaxCardsWhileHoldingPack = MaxHandSize - CardsPerBoosterPack;

    /// <summary>Vertical gap between flat cards in hand.</summary>
    public const float HandStackSpacing = 0.002f;

    public const float ScatterMinX = -5f;
    public const float ScatterMaxX = 5f;
    public const float ScatterMinZ = 0.5f;
    public const float ScatterMaxZ = 8.5f;
    public const float ScatterMinSpacing = 0.24f;

    /// <summary>Uniform scale for flat cards on the ground.</summary>
    public const float GroundCardScale = 1.3f;

    /// <summary>Uniform scale for shelf-placed cards (hand scale stays separate).</summary>
    public const float WorldCardScale = 1.1f;

    const float InteractionOutlineThicknessPercent = 0.07f;
    const float HandSelectionOutlineThicknessPercent = 0.024f;

    /// <summary>Card width in flat/root space (X).</summary>
    public static float Width => CardArtLibrary.FlatWidth;

    /// <summary>Card length in flat/root space (Z).</summary>
    public static float Height => CardArtLibrary.FlatHeight;

    /// <summary>Card thickness in flat/root space (Y).</summary>
    public static float Thickness => CardArtLibrary.FlatThickness;

    /// <summary>Yellow highlight border thickness around interactable cards.</summary>
    public static float InteractionOutlineThickness => Width * InteractionOutlineThicknessPercent;

    /// <summary>White border thickness for the selected hand card.</summary>
    public static float HandSelectionOutlineThickness => Width * HandSelectionOutlineThicknessPercent;
}

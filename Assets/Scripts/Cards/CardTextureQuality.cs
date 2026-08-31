/// <summary>
/// Texture resolution tier for card rendering.
/// </summary>
public enum CardTextureQuality
{
    /// <summary>GPU-instanced ground cards.</summary>
    World = 0,

    /// <summary>Hover, pickup flight, hand, and physics drops.</summary>
    Detail = 1,
}

public static class CardTextureSettings
{
    /// <summary>Instanced floor cards — matched to detail for video capture.</summary>
    public const int WorldMaxSize = 1024;

    public const int DetailMaxSize = 1024;
}

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
    public const int WorldMaxSize = 512;
    public const int DetailMaxSize = 1024;
}

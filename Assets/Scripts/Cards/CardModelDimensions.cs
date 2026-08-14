/// <summary>
/// Physical card size matching 1024×1434 art (5:7 portrait).
/// </summary>
public static class CardModelDimensions
{
    public const float ArtWidthPixels = 1024f;
    public const float ArtHeightPixels = 1434f;

    /// <summary>Card width in metres (~126 mm).</summary>
    public const float Width = 0.126f;

    public static float Height => Width * (ArtHeightPixels / ArtWidthPixels);

    /// <summary>Edge thickness in metres (~4 mm).</summary>
    public const float Thickness = 0.004f;

    /// <summary>Visible rounded corner on 1024×1434 card art (measured from PNG alpha).</summary>
    public const float ArtCornerRadiusPixels = 27.3f;

    /// <summary>Corner radius in mesh-local metres, matching the PNG silhouette.</summary>
    public static float CornerRadius => ArtCornerRadiusPixels / ArtWidthPixels * Width;
}

namespace Blitter;

/// <summary>
/// Texture sampling mode used when scaling pixels.
/// </summary>
public enum ImageSampling
{
    /// <summary>Bilinear filtering. Smooths magnified content; right for photos and most artwork.</summary>
    Linear = 0,

    /// <summary>Nearest-neighbor sampling. Preserves crisp pixel edges; right for pixel art and debug zoom views.</summary>
    Nearest = 1,
}

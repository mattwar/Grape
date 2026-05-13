namespace Blitter;

/// <summary>
/// An image with an explicit mip chain: a base level plus one or more
/// successively-halved lower-resolution levels supplied by the caller.
/// Used when each mip level carries different content (e.g. a
/// prefiltered specular environment map, where mip N is the
/// environment blurred for roughness N), rather than just a
/// downsampled copy of the base.
/// </summary>
/// <remarks>
/// <para>
/// For the much more common case of "I want renderers to generate a
/// mip chain by downsampling my image", set
/// <see cref="Image.Mipmaps"/> on a plain <see cref="Image"/> instead;
/// this type is only needed when the chain's contents are non-trivial.
/// </para>
/// <para>
/// Mip levels are zero-indexed: <see cref="Base"/> is level 0; each
/// subsequent level has dimensions <c>max(1, prev / 2)</c>. The chain
/// does not have to descend all the way to 1×1.
/// </para>
/// </remarks>
public sealed class MipmappedImage
{
    private MipmappedImage(IReadOnlyList<Image> levels)
    {
        Levels = levels;
    }

    /// <summary>
    /// The mip levels in order from highest to lowest resolution.
    /// <c>Levels[0]</c> is the base level.
    /// </summary>
    public IReadOnlyList<Image> Levels { get; }

    /// <summary>The highest-resolution level (mip 0).</summary>
    public Image Base => Levels[0];

    /// <summary>Number of mip levels in the chain. Always at least 1.</summary>
    public int LevelCount => Levels.Count;

    /// <summary>Width of the base level, in pixels.</summary>
    public int Width => Base.Size.Width;

    /// <summary>Height of the base level, in pixels.</summary>
    public int Height => Base.Size.Height;

    /// <summary>Pixel format shared by every level.</summary>
    public PixelFormat PixelFormat => Base.PixelFormat;

    /// <summary>
    /// Builds a mipmapped image from an ordered list of levels. The
    /// first entry is the base level; each subsequent entry must have
    /// dimensions <c>max(1, prev / 2)</c> and share the base's
    /// <see cref="Blitter.PixelFormat"/>.
    /// </summary>
    public static MipmappedImage Create(params Image[] levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        if (levels.Length < 1)
            throw new ArgumentException("MipmappedImage needs at least one level.", nameof(levels));

        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] is null)
                throw new ArgumentException($"Level {i} is null.", nameof(levels));
        }

        var baseLevel = levels[0];
        var (baseW, baseH) = baseLevel.Size;
        var format = baseLevel.PixelFormat;

        int expectedW = baseW;
        int expectedH = baseH;
        for (int i = 1; i < levels.Length; i++)
        {
            expectedW = Math.Max(1, expectedW >> 1);
            expectedH = Math.Max(1, expectedH >> 1);

            var (w, h) = levels[i].Size;
            if (w != expectedW || h != expectedH)
                throw new ArgumentException(
                    $"Level {i} is {w}x{h}; expected {expectedW}x{expectedH} (half of level {i - 1}, floored to 1).",
                    nameof(levels));
            if (levels[i].PixelFormat != format)
                throw new ArgumentException(
                    $"Level {i} has PixelFormat {levels[i].PixelFormat}; expected {format} to match the base.",
                    nameof(levels));
        }

        // Defensive copy so callers can't mutate the chain after construction.
        var copy = new Image[levels.Length];
        Array.Copy(levels, copy, levels.Length);
        return new MipmappedImage(copy);
    }

    /// <summary>
    /// Convenience: builds a single-level chain wrapping a plain
    /// <see cref="Image"/>. Useful when an API takes
    /// <c>MipmappedImage</c> uniformly but most callers only have one
    /// level.
    /// </summary>
    public static MipmappedImage FromBase(Image baseLevel)
    {
        ArgumentNullException.ThrowIfNull(baseLevel);
        return new MipmappedImage(new[] { baseLevel });
    }
}

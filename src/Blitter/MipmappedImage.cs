namespace Blitter;

/// <summary>
/// An image with an explicit mip chain: a base image plus one or more
/// successively-halved lower-resolution levels supplied by the caller.
/// </summary>
public sealed class MipmappedImage : Image
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

    /// <inheritdoc/>
    public override int LevelCount => Levels.Count;

    /// <inheritdoc/>
    public override int Width => Base.Width;

    /// <inheritdoc/>
    public override int Height => Base.Height;

    /// <inheritdoc/>
    public override PixelFormat PixelFormat => Base.PixelFormat;

    /// <inheritdoc/>
    public override bool Mipmaps => false;

    /// <inheritdoc/>
    public override bool IsDisposed => Base.IsDisposed;

    /// <inheritdoc/>
    public override int Version
    {
        get
        {
            unchecked
            {
                int v = 0;
                for (int i = 0; i < Levels.Count; i++)
                    v += Levels[i].Version;
                return v;
            }
        }
    }

    /// <inheritdoc/>
    public override void Invalidate()
    {
        for (int i = 0; i < Levels.Count; i++)
            Levels[i].Invalidate();
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        for (int i = 0; i < Levels.Count; i++)
            Levels[i].Dispose();
    }

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

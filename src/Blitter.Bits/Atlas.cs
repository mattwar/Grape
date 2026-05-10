namespace Blitter.Bits;

/// <summary>
/// An <see cref="Image"/> paired with a list of pixel-space rectangles
/// that name regions inside it. Useful for sprite sheets, glyph atlases,
/// and any other case where one texture upload backs many discrete
/// draws (each <see cref="Renderer2D.DrawImage(Image, Rect, Rect)"/>
/// uses the same GPU texture, so the cost is one upload + N draws).
/// Regions can be looked up by index and, optionally, by name.
/// </summary>
public sealed class Atlas : IDisposable
{
    private readonly Rect[] _rects;
    private readonly Dictionary<string, int>? _names;
    private readonly bool _ownsImage;
    private bool _disposed;

    /// <summary>The backing image. All region rectangles index into this image's pixel space.</summary>
    public Image Image { get; }

    /// <summary>Number of regions in the atlas.</summary>
    public int Count => _rects.Length;

    /// <summary>Looks up a region by zero-based index.</summary>
    public Rect this[int index] => _rects[index];

    /// <summary>
    /// Looks up a region by name. Throws <see cref="KeyNotFoundException"/>
    /// if the name was not registered, or <see cref="InvalidOperationException"/>
    /// if this atlas has no name map.
    /// </summary>
    public Rect this[string name]
    {
        get
        {
            if (_names is null)
                throw new InvalidOperationException("Atlas has no name map.");
            return _rects[_names[name]];
        }
    }

    /// <summary>
    /// Wraps an image with an explicit list of regions. By default the atlas
    /// takes ownership of <paramref name="image"/> and disposes it when the
    /// atlas is disposed; pass <paramref name="ownsImage"/> = <c>false</c>
    /// when the image is shared with other consumers.
    /// </summary>
    public Atlas(Image image, ReadOnlySpan<Rect> rects, bool ownsImage = true)
        : this(image, rects, names: null, ownsImage)
    {
    }

    /// <summary>
    /// Wraps an image with an explicit list of regions and a name-to-index
    /// map. The map is copied; the atlas does not retain a reference to the
    /// caller's dictionary.
    /// </summary>
    public Atlas(
        Image image,
        ReadOnlySpan<Rect> rects,
        IReadOnlyDictionary<string, int>? names,
        bool ownsImage = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        _rects = rects.ToArray();
        Image = image;
        _ownsImage = ownsImage;

        if (names is not null)
        {
            _names = new Dictionary<string, int>(names.Count, StringComparer.Ordinal);
            foreach (var kv in names)
            {
                if ((uint)kv.Value >= (uint)_rects.Length)
                    throw new ArgumentOutOfRangeException(nameof(names),
                        $"Name '{kv.Key}' maps to index {kv.Value} which is outside [0, {_rects.Length}).");
                _names.Add(kv.Key, kv.Value);
            }
        }
    }

    /// <summary>
    /// Splits <paramref name="image"/> into a uniform grid of
    /// <paramref name="columns"/> × <paramref name="rows"/> cells. Each
    /// cell becomes one region, indexed in row-major order
    /// (<c>row * columns + col</c>). Cell size is derived from the image
    /// size; use the other <c>Grid</c> overload when the image has padding
    /// rows/columns past the last cell.
    /// </summary>
    public static Atlas Grid(Image image, int columns, int rows, bool ownsImage = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
        var (w, h) = image.Size;
        return Grid(image, columns, rows, w / columns, h / rows, ownsImage);
    }

    /// <summary>
    /// Splits <paramref name="image"/> into a uniform grid using an
    /// explicit cell size. Use this overload when the image dimensions
    /// don't divide evenly, or when only the top-left
    /// (<paramref name="columns"/> * <paramref name="cellWidth"/>,
    /// <paramref name="rows"/> * <paramref name="cellHeight"/>) sub-region
    /// of the image is meaningful.
    /// </summary>
    public static Atlas Grid(
        Image image,
        int columns,
        int rows,
        int cellWidth,
        int cellHeight,
        bool ownsImage = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
        if (cellWidth <= 0) throw new ArgumentOutOfRangeException(nameof(cellWidth));
        if (cellHeight <= 0) throw new ArgumentOutOfRangeException(nameof(cellHeight));

        var rects = new Rect[columns * rows];
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                rects[row * columns + col] = new Rect(
                    col * cellWidth,
                    row * cellHeight,
                    cellWidth,
                    cellHeight);
            }
        }
        return new Atlas(image, rects, ownsImage);
    }

    /// <summary>
    /// True if a region with the given name is registered.
    /// </summary>
    public bool Contains(string name) => _names is not null && _names.ContainsKey(name);

    /// <summary>
    /// Resolves a name to its region index. Returns <c>false</c> if the
    /// atlas has no name map or the name is not registered.
    /// </summary>
    public bool TryGetIndex(string name, out int index)
    {
        if (_names is not null && _names.TryGetValue(name, out index))
            return true;
        index = -1;
        return false;
    }

    /// <summary>Draws region <paramref name="index"/> into <paramref name="destination"/>.</summary>
    public bool Draw(Renderer2D renderer, int index, Rect destination)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        return renderer.DrawImage(Image, _rects[index], destination);
    }

    /// <summary>Draws the named region into <paramref name="destination"/>.</summary>
    public bool Draw(Renderer2D renderer, string name, Rect destination)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        return renderer.DrawImage(Image, this[name], destination);
    }

    /// <summary>
    /// Disposes the backing <see cref="Image"/> if this atlas owns it.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsImage)
            Image.Dispose();
    }
}

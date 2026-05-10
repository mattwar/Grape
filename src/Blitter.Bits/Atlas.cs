namespace Blitter.Bits;

/// <summary>
/// A collection of regions over a single <see cref="Image"/>.
/// </summary>
public sealed class Atlas : IDisposable
{
    private readonly Rect[] _regions;
    private readonly Dictionary<string, int>? _names;
    private readonly bool _ownsImage;
    private bool _disposed;

    /// <summary>The backing image. All region rectangles index into this image's pixel space.</summary>
    public Image Image { get; }

    /// <summary>Number of regions in the atlas.</summary>
    public int Count => _regions.Length;

    /// <summary>Looks up a region by zero-based index.</summary>
    public Rect this[int index] => _regions[index];

    /// <summary>
    /// Looks up a region by name.
    /// </summary>
    public Rect this[string name]
    {
        get
        {
            if (_names is null)
                throw new InvalidOperationException("Atlas has no name map.");
            return _regions[_names[name]];
        }
    }

    /// <summary>
    /// Constructs an <see cref="Atlas"/> from an image and a set of regions.
    /// </summary>
    public Atlas(Image image, ReadOnlySpan<Rect> regions, bool ownsImage = true)
        : this(image, regions, names: null, ownsImage)
    {
    }

    /// <summary>
    /// Constructs an <see cref="Atlas"/> from an image, a set of regions, and an optional name-to-index map.   
    /// </summary>
    public Atlas(
        Image image,
        ReadOnlySpan<Rect> regions,
        IReadOnlyDictionary<string, int>? names,
        bool ownsImage = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        _regions = regions.ToArray();
        Image = image;
        _ownsImage = ownsImage;

        if (names is not null)
        {
            _names = new Dictionary<string, int>(names.Count, StringComparer.Ordinal);
            foreach (var kv in names)
            {
                if ((uint)kv.Value >= (uint)_regions.Length)
                    throw new ArgumentOutOfRangeException(nameof(names),
                        $"Name '{kv.Key}' maps to index {kv.Value} which is outside [0, {_regions.Length}).");
                _names.Add(kv.Key, kv.Value);
            }
        }
    }

    /// <summary>
    /// Creates an <see cref="Atlas"/> by splitting an <see cref="Image"/> into a uniform grid of regions.
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
    /// Creates an <see cref="Atlas"/> by splitting an <see cref="Image"/> into a uniform grid of regions.
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
        return renderer.DrawImage(Image, _regions[index], destination);
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

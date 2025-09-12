namespace SDL3.Model;

public sealed class Palette : IDisposable
{
    private nint _paletteId;
    private IReadOnlyList<SDL.Color> _colors;

    private Palette(nint paletteId, IReadOnlyList<SDL.Color> colors)
    {
        _paletteId = paletteId;
        _colors = colors;
    }

    internal Palette(nint paletteId)
        : this(paletteId, GetColors(paletteId))
    {
    }

    internal nint Id => _paletteId;

    private static IReadOnlyList<SDL.Color> GetColors(nint paletteId)
    {
        if (paletteId == 0)
            return Array.Empty<SDL.Color>();
        unsafe
        {
#pragma warning disable CS8500
            SDL.Palette* palette = (SDL.Palette*)paletteId;
#pragma warning restore CS8500
            return palette->Colors;
        }
    }

    private bool IsDisposed => _paletteId == 0;

    public void Dispose()
    {
        if (!IsDisposed)
        {
            var id = Interlocked.Exchange(ref _paletteId, 0);
            if (id != 0)
            {
                SDL.DestroyPalette(id);
            }
        }
    }

    /// <summary>
    /// The colors in the palette.
    /// </summary>
    public IReadOnlyList<SDL.Color> Colors => _colors;

    /// <summary>
    /// An empty palette
    /// </summary>
    public static readonly Palette Empty = new Palette(0);
}

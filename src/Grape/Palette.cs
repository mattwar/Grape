namespace Grape;

public sealed class Palette : IDisposable
{
    private nint _paletteId;
    private IReadOnlyList<Color> _colors;

    private Palette(nint paletteId, IReadOnlyList<Color> colors)
    {
        _paletteId = paletteId;
        _colors = colors;
    }

    internal Palette(nint paletteId)
        : this(paletteId, GetColors(paletteId))
    {
    }

    internal nint Id => _paletteId;

    private static IReadOnlyList<Color> GetColors(nint paletteId)
    {
        if (paletteId == 0)
            return Array.Empty<Color>();
        unsafe
        {
#pragma warning disable CS8500
            SDL.Palette* palette = (SDL.Palette*)paletteId;
#pragma warning restore CS8500
            var sdlColors = palette->Colors;
            var result = new Color[sdlColors.Length];
            for (int i = 0; i < sdlColors.Length; i++)
                result[i] = sdlColors[i];
            return result;
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
    public IReadOnlyList<Color> Colors => _colors;

    /// <summary>
    /// An empty palette
    /// </summary>
    public static readonly Palette Empty = new Palette(0);
}

namespace Blitter.Bits;

/// <summary>
/// An abstraction of gradient of colors over a range.
/// </summary>
public sealed class Gradient
{
    private readonly float[] _positions;
    private readonly Color[] _colors;

    /// <summary>
    /// Builds a gradient from pairs of positions and colors.
    /// The parameter <paramref name="stops"/> must have at least two elements, and the positions must be in ascending order.
    /// </summary>
    public Gradient(IEnumerable<(float Position, Color Color)> stops)
    {
        ArgumentNullException.ThrowIfNull(stops);
        var arr = stops.OrderBy(s => s.Position).ToArray();
        if (arr.Length < 2)
            throw new ArgumentException("A gradient requires at least two stops.", nameof(stops));
        _positions = new float[arr.Length];
        _colors = new Color[arr.Length];
        for (int i = 0; i < arr.Length; i++)
        {
            _positions[i] = arr[i].Position;
            _colors[i] = arr[i].Color;
        }
    }

    /// <summary>
    /// Constructs a gradient from an array of colors, evenly spaced over [0, 1].
    /// At least two colors are required.
    /// </summary>
    public static Gradient FromColors(params Color[] colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        if (colors.Length < 2)
            throw new ArgumentException("A gradient requires at least two colors.", nameof(colors));
        var stops = new (float, Color)[colors.Length];
        for (int i = 0; i < colors.Length; i++)
            stops[i] = (i / (float)(colors.Length - 1), colors[i]);
        return new Gradient(stops);
    }

    /// <summary>The number of color stops in this gradient.</summary>
    public int StopCount => _positions.Length;

    /// <summary>Samples the gradient at parameter <paramref name="t"/>.</summary>
    public Color Sample(float t)
    {
        if (t <= _positions[0]) return _colors[0];
        var last = _positions.Length - 1;
        if (t >= _positions[last]) return _colors[last];

        // Linear scan is fine for typical stop counts (< 16). If
        // someone ships a 256-stop heatmap we can switch to a binary
        // search later.
        for (int i = 0; i < last; i++)
        {
            var p0 = _positions[i];
            var p1 = _positions[i + 1];
            if (t <= p1)
            {
                var local = (t - p0) / (p1 - p0);
                return Color.Lerp(_colors[i], _colors[i + 1], local);
            }
        }
        return _colors[last];
    }
}

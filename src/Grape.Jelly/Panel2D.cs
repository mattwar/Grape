namespace Grape.Jelly;

public class Panel2D
{
    public Panel2D(Prop2D prop)
    {
        this.Prop = prop;
    }

    public Prop2D Prop { get; }

    /// <summary>
    /// The computed bounds of the panel within the window.
    /// </summary>
    internal Rect Bounds { get; set; }

    /// <summary>
    /// The measure type for Width.
    /// </summary>
    public Measure WidthMeasure { get; set; } = Measure.Proportion;

    /// <summary>
    /// The measure type for Height.
    /// </summary>
    public Measure HeightMeasure { get; set; } = Measure.Pixels;

    /// <summary>
    /// The width of the panel.
    /// </summary>
    public float Width { get; set; } = 100f;

    /// <summary>
    /// The height of the panel
    /// </summary>
    public float Height { get; set; } = 100f;

    /// <summary>
    /// Converts a <see cref="Prop"/> to a <see cref="Panel2D"/>.
    /// </summary>
    public static implicit operator Panel2D(Prop2D prop) => new Panel2D(prop);
}

public enum Measure
{
    /// <summary>
    /// The dimension is specified in pixels.
    /// </summary>
    Pixels,

    /// <summary>
    /// The dimension is based on a proportion of the remaining space in the parent container after fixed dimensions are allocated.
    /// </summary>
    Proportion
}

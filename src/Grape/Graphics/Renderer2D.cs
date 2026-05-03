using System.Numerics;

namespace Grape;

/// <summary>
/// A renderer that renders 2D graphics to a target.
/// </summary>
public abstract class Renderer2D
{
    #region State

    /// <summary>Clipping rectangle for subsequent draws.</summary>
    public abstract Rect ClipRect { get; set; }

    /// <summary>Per-channel color scale applied to draw colors.</summary>
    public abstract float ColorScale { get; set; }

    /// <summary>The current draw color.</summary>
    public abstract Color DrawColor { get; set; }

    /// <summary>Logical presentation rectangle for the current presentation mode.</summary>
    public abstract Rect LogicalRepresentationRect { get; }

    /// <summary>The output size in pixels.</summary>
    public abstract (int Width, int Height) OutputSize { get; }

    /// <summary>The rendering scale factors.</summary>
    public abstract (float ScaleX, float ScaleY) Scale { get; set; }

    /// <summary>The portion of the rendering target where draws are performed.</summary>
    public abstract Rect ViewPort { get; set; }

    #endregion

    #region Frame

    /// <summary>Fills the current draw target with <see cref="DrawColor"/>.</summary>
    public abstract void Clear();

    #endregion

    #region Drawing

    /// <summary>Draws debug text at the given location.</summary>
    public abstract bool RenderDebugText(int x, int y, string text, float scale = 0f);

    /// <summary>Renders a portion of <paramref name="image"/> to a destination rectangle.</summary>
    public abstract bool RenderImage(Image image, Rect source, Rect destination);

    /// <summary>Renders the entire <paramref name="image"/> to a destination rectangle.</summary>
    public bool RenderImage(Image image, Rect destination)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (w, h) = image.Size;
        return RenderImage(image, new Rect(0, 0, w, h), destination);
    }

    /// <summary>Renders the entire <paramref name="image"/> at a position with optional uniform scale.</summary>
    public bool RenderImage(Image image, float x, float y, float scale = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (w, h) = image.Size;
        var source = new Rect(0, 0, w, h);
        var destination = new Rect(x, y, w * scale, h * scale);
        return RenderImage(image, source, destination);
    }

    /// <summary>Renders a portion of <paramref name="image"/> rotated about <paramref name="center"/>.</summary>
    public abstract bool RenderImageRotated(Image image, Rect source, Rect destination, float angle, Vector2 center, FlipMode flip = FlipMode.None);

    /// <summary>Renders the entire <paramref name="image"/> rotated about <paramref name="center"/>.</summary>
    public bool RenderImageRotated(Image image, Rect destination, float angle, Vector2 center, FlipMode flip = FlipMode.None)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (w, h) = image.Size;
        return RenderImageRotated(image, new Rect(0, 0, w, h), destination, angle, center, flip);
    }

    /// <summary>Renders the entire <paramref name="image"/> at a position rotated about a center.</summary>
    public bool RenderImageRotated(Image image, float x, float y, float angle, float centerX, float centerY, float scale = 1.0f, FlipMode flip = FlipMode.None)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (w, h) = image.Size;
        var source = new Rect(0, 0, w, h);
        var destination = new Rect(x, y, w * scale, h * scale);
        var center = new Vector2(centerX * scale, centerY * scale);
        return RenderImageRotated(image, source, destination, angle, center, flip);
    }

    /// <summary>Fills <paramref name="rect"/> with <see cref="DrawColor"/>.</summary>
    public abstract bool RenderFillRect(Rect rect);

    /// <summary>Fills each rectangle in <paramref name="rects"/> with <see cref="DrawColor"/>.</summary>
    public abstract bool RenderFillRects(ReadOnlySpan<Rect> rects);

    /// <summary>
    /// Renders an indexed triangle list, optionally sampling from
    /// <paramref name="image"/>.
    /// </summary>
    public abstract bool RenderGeometry(ReadOnlySpan<Vertex2D> vertices, ReadOnlySpan<int> indices, Image? image = null);

    /// <summary>Draws a line between two points.</summary>
    public abstract bool RenderLine(float x1, float y1, float x2, float y2);

    /// <summary>Draws a connected polyline through <paramref name="points"/>.</summary>
    public abstract bool RenderLines(ReadOnlySpan<Vector2> points);

    /// <summary>Draws a single point.</summary>
    public abstract bool RenderPoint(float x, float y);

    /// <summary>Draws a set of points.</summary>
    public abstract bool RenderPoints(ReadOnlySpan<Vector2> points);

    #endregion
}

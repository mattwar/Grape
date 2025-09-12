using static SDL3.SDL;

namespace SDL3.Model;

public static class SurfaceExtensions
{
    //extension(Surface surface)
    //{
    //    /// <summary>
    //    /// Replace matching colors on the surface with a new color.
    //    /// </summary>
    //    /// <param name="oldColor"></param>
    //    /// <param name="newColor"></param>
    //    /// <param name="tolerance"></param>
    //    public void ReplaceMatchingColor(SDL.Color oldColor, SDL.Color newColor, int tolerance = ColorExtensions.DefaultColorTolerance)
    //    {
    //        surface.TransformPixels(context =>
    //        {
    //            if (context.Color.IsClosedTo(oldColor, tolerance))
    //            {
    //                context.Color = newColor;
    //            }
    //        });
    //    }

    //    /// <summary>
    //    /// Sets the alpha transparency for pixels matching the specified color.
    //    /// </summary>
    //    public void SetAlpha(byte alpha, SDL.Color color, int tolerance = ColorExtensions.DefaultColorTolerance)
    //    {
    //        surface.TransformPixels(context =>
    //        {
    //            if (context.Color.IsClosedTo(color, tolerance))
    //            {
    //                context.Color = context.Color.WithAlpha(alpha);
    //            }
    //        });
    //    }

    //    /// <summary>
    //    /// Transforms each pixel on the surface using the specified action.
    //    /// </summary>
    //    public void TransformPixels(Action<PixelContext> action)
    //    {
    //        var size = surface.Size;

    //        for (int y = 0; y < size.Height; y++)
    //        {
    //            for (int x = 0; x < size.Width; x++)
    //            {
    //                var context = new PixelContext(surface, x, y);
    //                action(context);
    //            }
    //        }
    //    }
    //}

    /// <summary>
    /// Replace matching colors on the surface with a new color.
    /// </summary>
    public static void ReplaceMatchingColor(this Surface surface, SDL.Color oldColor, SDL.Color newColor, int tolerance = ColorExtensions.DefaultColorTolerance)
    {
        surface.TransformPixels(context =>
        {
            if (context.Color.IsClosedTo(oldColor, tolerance))
            {
                context.Color = newColor;
            }
        });
    }

    /// <summary>
    /// Sets the alpha transparency for pixels matching the specified color.
    /// </summary>
    public static void SetAlpha(this Surface surface, byte alpha, SDL.Color color, int tolerance = ColorExtensions.DefaultColorTolerance)
    {
        surface.TransformPixels(context =>
        {
            if (context.Color.IsClosedTo(color, tolerance))
            {
                context.Color = context.Color.WithAlpha(alpha);
            }
        });
    }

    /// <summary>
    /// Transforms each pixel on the surface using the specified action.
    /// </summary>
    public static void TransformPixels(this Surface surface, Action<PixelContext> action)
    {
        var size = surface.Size;

        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                var context = new PixelContext(surface, x, y);
                action(context);
            }
        }
    }

}

public struct PixelContext
{
    private readonly Surface _surface;

    public int X { get; }
    public int Y { get; }

    internal PixelContext(Surface surface, int x, int y)
    {
        _surface = surface;
        X = x;
        Y = y;
    }

    private SDL.Color? _color;

    public SDL.Color Color
    {
        get
        {
            if (_color == null)
                _color = _surface.GetPixel(X, Y);
            return _color.Value;
        }

        set
        {
            _color = value;
            _surface.SetPixel(X, Y, value);
        }
    }
}


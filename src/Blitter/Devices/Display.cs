using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Blitter.Devices;

/// <summary>
/// Discovery and lookup of available <see cref="DisplayDevice"/>s.
/// </summary>
public static class Display
{
    private static ImmutableList<DisplayDevice>? _displays = null;

    /// <summary>
    /// The available displays.
    /// </summary>
    public static ImmutableList<DisplayDevice> Displays
    {
        get
        {
            var displays = _displays;
            if (displays == null)
            {
                var ids = SDL.GetDisplays(out var count);
                if (ids != null && count > 0)
                {
                    displays = ids.Select(id => new DisplayDevice(id)).ToImmutableList();
                }
                else
                {
                    displays = ImmutableList<DisplayDevice>.Empty;
                }
                Interlocked.CompareExchange(ref _displays, displays, null);
            }
            return _displays!;
        }
    }

    /// <summary>
    /// The primary display.
    /// </summary>
    public static DisplayDevice Primary
    {
        get
        {
            var id = SDL.GetPrimaryDisplay();
            if (DisplayDevice.TryGetDisplay(id, out var display))
            {
                return display;
            }
            throw new InvalidOperationException("No primary display found.");
        }
    }

    /// <summary>
    /// Gets the display associated with the given point in the multi-display space.
    /// </summary>
    public static bool TryGetDisplayFromPoint(Vector2 point, [NotNullWhen(true)] out DisplayDevice? display)
    {
        var sdlPoint = new SDL.Point { X = (int)point.X, Y = (int)point.Y };
        var id = SDL.GetDisplayForPoint(sdlPoint);
        return DisplayDevice.TryGetDisplay(id, out display);
    }

    /// <summary>
    /// Gets the display associated with the given rectangle in the multi-display space.
    /// </summary>
    public static bool TryGetDisplayFromRect(Rect rect, [NotNullWhen(true)] out DisplayDevice? display)
    {
        SDL.Rect sdlRect = rect;
        var id = SDL.GetDisplayForRect(sdlRect);
        return DisplayDevice.TryGetDisplay(id, out display);
    }
}

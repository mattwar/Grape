using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SDL3.Model;

public sealed class Display
{
    private uint _displayId;

    internal Display(uint displayId)
    {
        _displayId = displayId;
    }

    public string Name => 
        SDL.GetDisplayName(_displayId) ?? "";

    /// <summary>
    /// The bounds of the display in multi-display space.
    /// </summary>
    public SDL.Rect Bounds => 
        SDL.GetDisplayBounds(_displayId, out var bounds) 
            ? bounds 
            : new SDL.Rect { X = 0, Y = 0, W = 0, H = 0 };

    /// <summary>
    /// The bounds of the display less the area occupied by system UI elements like the task bar or menu bar.
    /// </summary>
    public SDL.Rect UsableBounds => 
        SDL.GetDisplayUsableBounds(_displayId, out var bounds) 
            ? bounds 
            : new SDL.Rect { X = 0, Y = 0, W = 0, H = 0 };

    /// <summary>
    /// The scaling for UI elements based of the DPI of the display.
    /// </summary>
    public float ContentScale =>
        SDL.GetDisplayContentScale(_displayId);

    /// <summary>
    /// The current display mode.
    /// </summary>
    public DisplayMode DisplayMode =>
        SDL.GetCurrentDisplayMode(_displayId) is { } mode
            ? new DisplayMode(mode)
            : default;

    /// <summary>
    /// The display mode used when the display is showing the desktop.
    /// </summary>
    public DisplayMode DesktopDisplayMode =>
        SDL.GetDesktopDisplayMode(_displayId) is { } mode
            ? new DisplayMode(mode)
            : default;

    /// <summary>
    /// All display modes supported in fullscreen for this display.
    /// </summary>
    public ImmutableList<DisplayMode> FullScreenDisplayModes
    {
        get
        {
            var displayModes = _displayModes;
            if (displayModes == null)
            {
                displayModes = SdlDisplayEx.GetFullScreenDisplayModes(_displayId);
                Interlocked.CompareExchange(ref _displayModes, displayModes, null);
            }
            return _displayModes!;
        }
    }

    private ImmutableList<DisplayMode>? _displayModes = null;

    /// <summary>
    /// The natural orientation of the display.
    /// </summary>
    public SDL.DisplayOrientation NaturalOrientation =>
        SDL.GetNaturalDisplayOrientation(_displayId);

    /// <summary>
    /// The current orientation of the display.
    /// </summary>
    public SDL.DisplayOrientation Orientation =>
        SDL.GetCurrentDisplayOrientation(_displayId);

    /// <summary>
    /// Gets the closest matching fullscreen display mode for the given parameters.
    /// </summary>
    public DisplayMode GetClosestFullScreenDisplayMode(int width, int height, float refreshRate = 60f, bool includingHighDensityModes = false)
    {
        if (SDL.GetClosestFullscreenDisplayMode(_displayId, width, height, refreshRate, includingHighDensityModes, out var dm))
        {
            return new DisplayMode(dm);
        }
        else
        {
            return this.DesktopDisplayMode;
        }
    }

    /// <summary>
    /// Gets the display associated with the given point in the multi-display space.
    /// </summary>
    public static bool TryGetDisplayFromPoint(SDL.Point point, [NotNullWhen(true)] out Display? display)
    {
        var id = SDL.GetDisplayForPoint(point);
        return TryGetDisplay(id, out display);
    }

    /// <summary>
    /// Gets the display associated with the given rectangle in the multi-display space.
    /// </summary>
    public static bool TryGetDisplayFromRect(SDL.Rect rect, [NotNullWhen(true)] out Display? display)
    {
        var id = SDL.GetDisplayForRect(rect);
        return TryGetDisplay(id, out display);
    }

    /// <summary>
    /// Gets the display associcated with the given id.
    /// </summary>
    internal static bool TryGetDisplay(uint id, [NotNullWhen(true)] out Display? display)
    {
        display = Displays.FirstOrDefault(d => d._displayId == id);
        return display != null;
    }

    /// <summary>
    /// The available displays.
    /// </summary>
    public static ImmutableList<Display> Displays
    {
        get
        {
            var displays = _displays;
            if (displays == null)
            {
                var ids = SDL.GetDisplays(out var count);
                if (ids != null && count > 0)
                {
                    displays = ids.Select(id => new Display(id)).ToImmutableList();
                }
                else
                {
                    displays = ImmutableList<Display>.Empty;
                }
                Interlocked.CompareExchange(ref _displays, displays, null);
            }
            return _displays!;
        }
    }

    private static ImmutableList<Display>? _displays = null;

    /// <summary>
    /// The primary display.
    /// </summary>
    public static Display PrimaryDisplay
    {
        get
        {
            var id = SDL.GetPrimaryDisplay();
            if (TryGetDisplay(id, out var display))
            {
                return display;
            }
            throw new InvalidOperationException("No primary display found.");
        }
    }
}

[System.Diagnostics.DebuggerDisplay("{Width}x{Height} {RefreshRate}Hz, Format={Format}, Density={PixelDensity}")]
public struct DisplayMode
{
    internal readonly SDL.DisplayMode _mode;

    internal DisplayMode(SDL.DisplayMode mode)
    {
        _mode = mode;
    }

    public int Width => _mode.W;
    public int Height => _mode.H;
    public float RefreshRate => _mode.RefreshRate;
    public SDL.PixelFormat Format => _mode.Format;
    public float PixelDensity => _mode.PixelDensity;
}

internal static partial class SdlDisplayEx
{
    [LibraryImport("SDL3", EntryPoint = "SDL_GetFullscreenDisplayModes"), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr SDL_GetFullscreenDisplayModes(uint displayID, out int count);

    internal static ImmutableList<DisplayMode> GetFullScreenDisplayModes(uint displayId)
    {
        var modesPtr = SDL_GetFullscreenDisplayModes(displayId, out var count);
        if (modesPtr != IntPtr.Zero && count > 0)
        {
            var builder = ImmutableList<DisplayMode>.Empty.ToBuilder();
            unsafe
            {
                SDL.DisplayMode* ptr = (SDL.DisplayMode*)Marshal.ReadIntPtr(modesPtr);
                for (; ptr != null && builder.Count < count; ptr++)
                {
                    builder.Add(new DisplayMode(*ptr));
                }
            }
            SDL.Free(modesPtr);
            return builder.ToImmutable();
        }
        else
        {
            return ImmutableList<DisplayMode>.Empty;
        }
    }
}

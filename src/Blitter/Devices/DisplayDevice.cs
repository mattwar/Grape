using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blitter.Devices;

/// <summary>
/// Represents a physical or virtual display device (aka Monitor) in a multi-display environment.
/// </summary>
public sealed class DisplayDevice
{
    internal readonly uint _displayId;

    internal DisplayDevice(uint displayId)
    {
        _displayId = displayId;
    }

    /// <summary>
    /// The name of the display.
    /// </summary>
    public string Name => 
        SDL.GetDisplayName(_displayId) ?? "";

    /// <summary>
    /// The bounds of the display in multi-display space.
    /// </summary>
    public Rect Bounds => 
        SDL.GetDisplayBounds(_displayId, out var bounds) 
            ? bounds 
            : default;

    /// <summary>
    /// The bounds of the display less the area occupied by system UI elements like the task bar or menu bar.
    /// </summary>
    public Rect UsableBounds => 
        SDL.GetDisplayUsableBounds(_displayId, out var bounds) 
            ? bounds 
            : default;

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
    public DisplayOrientation NaturalOrientation =>
        (DisplayOrientation)SDL.GetNaturalDisplayOrientation(_displayId);

    /// <summary>
    /// The current orientation of the display.
    /// </summary>
    public DisplayOrientation Orientation =>
        (DisplayOrientation)SDL.GetCurrentDisplayOrientation(_displayId);

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
    /// Gets the display associated with the given id.
    /// </summary>
    internal static bool TryGetDisplay(uint id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DisplayDevice? display)
    {
        display = Display.Displays.FirstOrDefault(d => d._displayId == id);
        return display != null;
    }
}

internal static partial class SdlDisplayEx
{
    /// <summary>
    /// This is remapped because SDL3 has a bug in the equivalent SDL.GetFullscreenDisplayModes function.
    /// </summary>
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

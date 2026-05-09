namespace Blitter.Devices;

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
    public PixelFormat Format => (PixelFormat)_mode.Format;
    public float PixelDensity => _mode.PixelDensity;
}

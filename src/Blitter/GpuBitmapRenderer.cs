namespace Blitter;

/// <summary>
/// A 3D renderer that draws into a <see cref="GpuBitmap"/>. The
/// target already lives on the GPU, so this path has no scratch
/// texture, no upload-wallpaper staging, and no download readback --
/// rendering writes straight into the bitmap's underlying texture.
/// </summary>
internal sealed class GpuBitmapRenderer : GpuRenderer
{
    private readonly GpuBitmap _target;
    private readonly SDL.GPUTextureFormat _format;
    private readonly uint _width;
    private readonly uint _height;

    internal GpuBitmapRenderer(GpuDevice device, GpuBitmap target)
        : base(device)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.IsDisposed)
            throw new ObjectDisposedException(nameof(GpuBitmap));

        _target = target;
        _format = GpuPixelFormatMap.ToGpu(target.PixelFormat);
        _width = (uint)target.Width;
        _height = (uint)target.Height;
    }

    /// <summary>
    /// Configures the renderer for either a clear-then-draw frame
    /// (<paramref name="backgroundColor"/> non-null) or a load-and-add
    /// frame that preserves the bitmap's existing pixels
    /// (<paramref name="backgroundColor"/> null). Must be called
    /// before <see cref="Renderer3D.Render"/>.
    /// </summary>
    internal void Configure(Color? backgroundColor)
    {
        if (backgroundColor is { } c)
        {
            BackgroundColor = c;
            AutoClear = true;
        }
        else
        {
            // LoadOp.Load preserves whatever's currently in the GPU
            // texture, which is exactly the wallpaper we want -- no
            // upload needed because the pixels already live there.
            AutoClear = false;
        }
    }

    protected override bool TryAcquireColorTarget(
        GpuRenderFrame frame,
        out GpuTexture? colorTarget,
        out SDL.GPUTextureFormat colorFormat,
        out uint width,
        out uint height,
        out uint layer,
        out uint mipLevel)
    {
        layer = 0;
        mipLevel = 0;
        if (_target.IsDisposed)
        {
            colorTarget = null;
            colorFormat = SDL.GPUTextureFormat.Invalid;
            width = 0;
            height = 0;
            return false;
        }

        colorTarget = _target.Texture;
        colorFormat = _format;
        width = _width;
        height = _height;
        return true;
    }

    protected override float GetTargetAspectRatio() =>
        _height > 0 ? (float)_width / _height : base.GetTargetAspectRatio();

    protected override (int Width, int Height) GetTargetSize() =>
        ((int)_width, (int)_height);
}

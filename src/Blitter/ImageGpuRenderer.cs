namespace Blitter;

/// <summary>
/// A 3D renderer that draws into an image.
/// </summary>
internal sealed class ImageGpuRenderer : GpuRenderer
{
    // SDL_GPU's R8G8B8A8Unorm color targets store pixels as in-memory
    // bytes R, G, B, A. SDL's PixelFormat.ABGR8888 -- a packed uint32
    // with A in the high byte -- has the same byte order on
    // little-endian platforms, so we can memcpy texels straight into
    // the surface (the "fast path"). RGBA64Float maps to
    // R16G16B16A16Float and also direct-copies because the half-float
    // byte order matches. Any other PixelFormat takes the "slow path":
    // the GPU target stays R8G8B8A8Unorm and per-pixel Get/Set against
    // the user's image converts to/from the surface's native layout.

    private readonly Bitmap _image;
    private readonly uint _width;
    private readonly uint _height;
    private readonly bool _directCopy;
    private readonly SDL.GPUTextureFormat _colorTargetFormat;
    // Bytes per pixel for the GPU color target. Equals the image's
    // BytesPerPixel on the direct-copy path; equals 4 on the slow path
    // (the GPU target is R8G8B8A8Unorm regardless).
    private readonly uint _gpuBpp;
    private GpuTexture? _ownedColorTarget;
    private GpuDownloadBuffer? _downloadBuffer;
    // Lazily created when the user requests additive rendering: holds
    // the image's existing pixels so they can be uploaded into the GPU
    // target before the render pass and preserved underneath new draws.
    private GpuUploadBuffer? _wallpaperBuffer;
    // Slow-path scratch: an R,G,B,A byte buffer matching the GPU
    // target's layout. Used for both staging the wallpaper upload and
    // receiving the downloaded pixels before we per-pixel-convert them
    // into the user's image.
    private byte[]? _scratch;
    private bool _uploadWallpaper;

    internal ImageGpuRenderer(GpuDevice device, Bitmap image)
        : base(device)
    {
        ArgumentNullException.ThrowIfNull(image);

        _image = image;
        _width = (uint)image.Size.Width;
        _height = (uint)image.Size.Height;
        (_colorTargetFormat, _directCopy) = MapPixelFormatToGpu(image.PixelFormat);
        _gpuBpp = _directCopy ? (uint)image.BytesPerPixel : 4u;

        var pixelBytes = checked(_width * _height * _gpuBpp);

        _ownedColorTarget = device.CreateTexture(new GpuTextureCreateInfo
        {
            Type = SDL.GPUTextureType.Texturetype2D,
            Format = _colorTargetFormat,
            // Sampler usage is included so the resulting texture could
            // be re-bound as input to a later pass on the same device;
            // ColorTarget alone would be enough for write-only usage.
            Usage = SDL.GPUTextureUsageFlags.ColorTarget | SDL.GPUTextureUsageFlags.Sampler,
            Width = _width,
            Height = _height,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            SampleCount = SDL.GPUSampleCount.SampleCount1,
        });
        _downloadBuffer = GpuDownloadBuffer.Create(device, pixelBytes);
    }

    protected override float GetTargetAspectRatio() =>
        _height > 0 ? (float)_width / _height : base.GetTargetAspectRatio();

    protected override (int Width, int Height) GetTargetSize() =>
        ((int)_width, (int)_height);

    // Picks the GPU color target format for an image of the given
    // surface pixel format, and reports whether the GPU bytes and the
    // surface bytes have identical layout (so a render pass result can
    // be memcpy'd straight into the surface). Throws for formats with
    // no GPU equivalent we know how to render into.
    private static (SDL.GPUTextureFormat Format, bool DirectCopy) MapPixelFormatToGpu(PixelFormat format) => 
        format switch
        {
            // Surface bytes R, G, B, A; matches R8G8B8A8Unorm exactly.
            PixelFormat.ABGR8888 => (SDL.GPUTextureFormat.R8G8B8A8Unorm, true),

            // Surface bytes R, G, B, A as little-endian half-floats;
            // matches R16G16B16A16Float exactly.
            PixelFormat.RGBA64Float => (SDL.GPUTextureFormat.R16G16B16A16Float, true),

            // Other 8-bit-per-channel layouts: render into R8G8B8A8Unorm
            // and pay per-pixel conversion on upload/download.
            PixelFormat.ARGB8888 or
            PixelFormat.BGRA8888 or
            PixelFormat.RGBA8888 or
            PixelFormat.XRGB8888 or
            PixelFormat.XBGR8888 or
            PixelFormat.RGBX8888 or
            PixelFormat.BGRX8888 => (SDL.GPUTextureFormat.R8G8B8A8Unorm, false),

            _ => throw new NotSupportedException(
                $"Pixel format {format} is not supported as a GPU render target. " +
                "Use ABGR8888 (default) or RGBA64Float, or one of the 8-bit-per-" +
                "channel RGBA variants."),
        };

    /// <summary>
    /// Configures the renderer for either a clear-then-draw frame
    /// (<paramref name="backgroundColor"/> non-null) or an additive
    /// frame that preserves the image's existing pixels as wallpaper
    /// (<paramref name="backgroundColor"/> null). Must be called
    /// before <see cref="Renderer3D.Render"/>.
    /// </summary>
    /// <remarks>
    /// Translucent alpha values are not blended over the wallpaper on
    /// this path -- the buffer is cleared to the literal RGBA you
    /// provide. Blending a translucent background will be supported
    /// once the GPU renderer gains a full-surface tint pass.
    /// </remarks>
    internal void Configure(Color? backgroundColor)
    {
        // TODO: Honor translucent backgroundColor by blending it over
        // the wallpaper. Needs a full-surface tint pass: keep
        // _uploadWallpaper = true, use LoadOp.Load, then draw a
        // screen-filling quad with the tint color through the normal
        // alpha-blended pipeline before the user's draws. Until that
        // exists, alpha < 255 here just clears to the literal RGBA.
        if (backgroundColor is { } c)
        {
            BackgroundColor = c;
            AutoClear = true;
            _uploadWallpaper = false;
        }
        else
        {
            // LoadOp.Load preserves whatever's in the texture at pass
            // start; OnBeforeUploads stages the image's pixels into it
            // first so "what's in the texture" is the wallpaper.
            AutoClear = false;
            _uploadWallpaper = true;
        }
    }

    protected override void OnBeforeUploads(GpuCopyPass copyPass)
    {
        if (!_uploadWallpaper || _ownedColorTarget is null || _ownedColorTarget.IsDisposed)
            return;

        var byteCount = checked(_width * _height * _gpuBpp);

        // Allocate the upload staging buffer once per renderer
        // instance; reused across frames if the user calls Render
        // multiple times.
        _wallpaperBuffer ??= (GpuUploadBuffer)GpuUploadBuffer.Create(Device, byteCount);

        ReadOnlySpan<byte> source;
        if (_directCopy)
        {
            // Fast path: image bytes already in R,G,B,A order.
            var pixels = _image.GetPixels();
            source = pixels.Length == (int)byteCount ? pixels : pixels[..(int)byteCount];
        }
        else
        {
            // Slow path: per-pixel convert the image's current contents
            // into the GPU target's R,G,B,A byte layout.
            var scratch = EnsureScratch((int)byteCount);
            FillScratchFromImage(scratch);
            source = scratch;
        }

        copyPass.UploadToTexture(
            _wallpaperBuffer!,
            _ownedColorTarget,
            _width,
            _height,
            source);
    }

    protected override bool TryAcquireColorTarget(
        GpuRenderFrame frame,
        out GpuTexture? colorTarget,
        out SDL.GPUTextureFormat colorFormat,
        out uint width,
        out uint height)
    {
        if (_ownedColorTarget is null || _ownedColorTarget.IsDisposed)
        {
            colorTarget = null;
            colorFormat = SDL.GPUTextureFormat.Invalid;
            width = 0;
            height = 0;
            return false;
        }

        colorTarget = _ownedColorTarget;
        colorFormat = _colorTargetFormat;
        width = _width;
        height = _height;
        return true;
    }

    protected override void PresentFrame()
    {
        var frame = CurrentFrame!;
        var color = CurrentColorTarget;

        if (color is null || _downloadBuffer is null)
        {
            // Nothing to download (acquisition failed); just submit the
            // empty/clear-only command buffer to release resources.
            frame.Submit();
            return;
        }

        if (!frame.TryBeginCopyPass(out var copyPass))
        {
            frame.Submit();
            return;
        }

        using (copyPass)
        {
            copyPass!.DownloadFromTexture(color, _downloadBuffer, _width, _height);
        }

        // Synchronous wait on the fence: when this returns the GPU has
        // finished the render + copy and the download buffer holds the
        // pixel bytes. Acceptable for the screenshot-shaped use case;
        // not appropriate for per-frame reads.
        using var fence = frame.SubmitAndAcquireFence();
        fence.Wait();

        var pixelBytes = checked((int)(_width * _height * _gpuBpp));
        if (_directCopy)
        {
            // Fast path: GPU's R,G,B,A bytes match the image's
            // ABGR8888 layout, so blit straight into the surface.
            _downloadBuffer.Read(_image.WritablePixels.Slice(0, pixelBytes));
        }
        else
        {
            // Slow path: read into scratch, then per-pixel convert
            // into the user's image. Same approach our SkiaSharp
            // bridge uses for cross-format copies.
            var scratch = EnsureScratch(pixelBytes);
            _downloadBuffer.Read(scratch.AsSpan(0, pixelBytes));
            WriteScratchToImage(scratch);
        }

        _image.Invalidate();
    }

    private byte[] EnsureScratch(int size)
    {
        if (_scratch is null || _scratch.Length < size)
            _scratch = new byte[size];
        return _scratch;
    }

    private void FillScratchFromImage(byte[] scratch)
    {
        int w = (int)_width, h = (int)_height;
        int i = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var c = _image.GetPixel(x, y);
                scratch[i++] = c.R;
                scratch[i++] = c.G;
                scratch[i++] = c.B;
                scratch[i++] = c.A;
            }
        }
    }

    private void WriteScratchToImage(byte[] scratch)
    {
        int w = (int)_width, h = (int)_height;
        int i = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var color = new Color(scratch[i], scratch[i + 1], scratch[i + 2], scratch[i + 3]);
                _image.SetPixel(x, y, color);
                i += 4;
            }
        }
    }

    public override void Dispose()
    {
        // Skip the base implicit-flush: the image-bound user model is
        // "draw, Render(), dispose", and disposing should not silently
        // emit another GPU frame.
        _ownedColorTarget?.Dispose();
        _downloadBuffer?.Dispose();
        _wallpaperBuffer?.Dispose();
        _ownedColorTarget = null;
        _downloadBuffer = null;
        _wallpaperBuffer = null;
    }
}

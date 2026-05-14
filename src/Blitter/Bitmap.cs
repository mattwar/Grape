namespace Blitter;

/// <summary>
/// Represents an image bitmap in memory.
/// </summary>
public sealed class Bitmap : Image
{
    private readonly Application _application;
    internal nint _imageId;
    private int _version;

    private Bitmap(nint imageId, bool mipmaps = false)
    {
        _imageId = imageId;
        _application = Application.Current;
        _application.AddResource(this);
        _version = 1;
        Mipmaps = mipmaps;
    }

    /// <summary>
    /// If true, the GPU is hinted to generate a mipmap chain for this image.
    /// </summary>
    public override bool Mipmaps { get; }

    /// <inheritdoc/>
    public override int LevelCount => 1;

    /// <inheritdoc/>
    public override int Width
    {
        get
        {
            if (IsDisposed)
                return default;
            unsafe
            {
                SDL.Surface* surface = (SDL.Surface*)_imageId;
                return surface->Width;
            }
        }
    }

    /// <inheritdoc/>
    public override int Height
    {
        get
        {
            if (IsDisposed)
                return default;
            unsafe
            {
                SDL.Surface* surface = (SDL.Surface*)_imageId;
                return surface->Height;
            }
        }
    }

    /// <summary>
    /// Creates a new image of the given size. The default format,
    /// <see cref="PixelFormat.ABGR8888"/>, is the 32-bpp RGBA layout used
    /// throughout Blitter; pass an explicit format only for specialized
    /// surfaces (paletted, YUV, 16-bit, float, etc.).
    /// </summary>
    public static new Bitmap Create(int width, int height, PixelFormat format = PixelFormat.ABGR8888, bool mipmaps = false)
    {
        var imageId = SDL.CreateSurface(width, height, (SDL.PixelFormat)format);
        if (imageId == 0)
            throw new InvalidOperationException("Cannot create image");
        return new Bitmap(imageId, mipmaps);
    }

    /// <inheritdoc/>
    public override bool IsDisposed => _imageId == 0;

    /// <summary>
    /// Bumped each time the image's contents change. Renderers use this to
    /// detect when their cached GPU texture upload is stale. If you mutate
    /// the image through raw pixel access (e.g. <see cref="GetPixels"/> or
    /// an external software renderer), call <see cref="Invalidate"/> to mark
    /// the change.
    /// </summary>
    public override int Version => _version;

    /// <summary>
    /// Marks the image contents as changed so renderers re-upload their
    /// cached GPU texture on the next draw. Mutations through
    /// <see cref="SetPixel"/> or <see cref="TransparentColor"/> bump the
    /// version automatically; this is for callers that touch raw pixels.
    /// </summary>
    public override void Invalidate()
    {
        if (IsDisposed)
            return;
        unchecked { _version++; }
    }

    internal void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(Bitmap));
    }

    /// <summary>
    /// Render into this image using a 2D renderer abstraction. The
    /// image's existing pixels are preserved as a backdrop and draws
    /// compose on top of them.
    /// </summary>
    /// <param name="renderAction">Callback that issues draws on the renderer.</param>
    public void Render2D(Action<Renderer2D> renderAction)
    {
        ArgumentNullException.ThrowIfNull(renderAction);
        ThrowIfDisposed();
        using var renderer = Renderer.Create(this);

        // Skip the implicit clear so existing surface pixels stay
        // visible underneath the new draws.
        renderer.AutoClear = false;

        renderAction(renderer);
        renderer.Render();
        Invalidate();
    }

    /// <summary>
    /// Render into this image using a 2D renderer abstraction, painting
    /// <paramref name="backgroundColor"/> behind the draws.
    /// </summary>
    /// <param name="backgroundColor">
    /// The background painted behind the draws. An opaque alpha (255)
    /// replaces the prior content (cheapest); a translucent alpha
    /// blends over the prior content using standard source-over
    /// compositing.
    /// </param>
    /// <param name="renderAction">Callback that issues draws on the renderer.</param>
    public void Render2D(Color backgroundColor, Action<Renderer2D> renderAction)
    {
        ArgumentNullException.ThrowIfNull(renderAction);
        ThrowIfDisposed();
        using var renderer = Renderer.Create(this);

        if (backgroundColor.A == 255)
        {
            // Opaque background: replace prior content via the
            // built-in clear -- cheaper than a full-surface FillRect
            // and produces an identical result.
            renderer.BackgroundColor = backgroundColor;
            renderer.AutoClear = true;
        }
        else
        {
            // Translucent background: blend the color over the
            // existing surface using SrcOver. AutoClear stays off so
            // the prior pixels survive into the FillRect.
            renderer.AutoClear = false;
            var savedBlend = renderer.BlendMode;
            var savedColor = renderer.DrawColor;
            renderer.BlendMode = SDL.BlendMode.Blend;
            renderer.DrawColor = backgroundColor;
            var (w, h) = Size;
            renderer.DrawFillRect(new Rect(0, 0, w, h));
            renderer.DrawColor = savedColor;
            renderer.BlendMode = savedBlend;
        }

        renderAction(renderer);
        renderer.Render();
        Invalidate();
    }

    /// <summary>
    /// Render into this image using a 3D renderer. The image's
    /// existing pixels are preserved as a backdrop and new draws
    /// compose on top of them; depth is per-call, so the wallpaper
    /// itself never occludes new draws. The call is synchronous:
    /// when it returns, the image's pixels reflect the final GPU
    /// output. Intended for screenshot- and thumbnail-shaped use
    /// cases, not per-frame readback.
    /// </summary>
    /// <param name="renderAction">Callback that issues draws on the renderer.</param>
    /// <remarks>
    /// Images in <see cref="PixelFormat.ABGR8888"/> take a fast path
    /// that memcpys pixels between the GPU target and the surface.
    /// Other formats fall back to a per-pixel conversion loop.
    /// </remarks>
    public void Render3D(Action<Renderer3D> renderAction)
        => Render3DCore(null, renderAction);

    /// <summary>
    /// Render into this image using a 3D renderer, painting
    /// <paramref name="backgroundColor"/> behind the draws. The call
    /// is synchronous: when it returns, the image's pixels reflect
    /// the final GPU output.
    /// </summary>
    /// <param name="backgroundColor">The background painted behind the draws.</param>
    /// <param name="renderAction">Callback that issues draws on the renderer.</param>
    /// <remarks>
    /// Translucent alpha values are currently treated as opaque on
    /// the 3D path (the alpha channel is honored in the cleared
    /// buffer but does not blend over prior pixels). Blending a
    /// translucent background over the wallpaper will be supported
    /// once the GPU renderer gains a full-surface tint pass.
    /// </remarks>
    public void Render3D(Color backgroundColor, Action<Renderer3D> renderAction)
        => Render3DCore(backgroundColor, renderAction);

    private void Render3DCore(Color? backgroundColor, Action<Renderer3D> renderAction)
    {
        ArgumentNullException.ThrowIfNull(renderAction);
        ThrowIfDisposed();
        using var renderer = new BitmapRenderer3D(GpuDevice.Default, this);
        renderer.Configure(backgroundColor);
        renderAction(renderer);
        renderer.Render();
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (!IsDisposed)
        {
            var id = Interlocked.Exchange(ref _imageId, 0);
            if (id != 0)
            {
                SDL.DestroySurface(id);
                _application.RemoveResource(this);

                var pal = Interlocked.CompareExchange(ref _palette, null, null);
                if (pal != null)
                {
                    pal.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Loads an image from disk. Decoding is handled by SkiaSharp,
    /// covering PNG, JPG, WebP, GIF, BMP, and the rest of SkiaSharp's
    /// supported formats. The decoded image is normalized to
    /// <see cref="PixelFormat.ABGR8888"/>.
    /// </summary>
    /// <param name="filePath">Path to the image file.</param>
    /// <param name="mipmaps">
    /// When true, allocates mipmap storage and regenerates mip levels
    /// from the base image. Recommended for textures sampled at
    /// varying distances; unnecessary for full-resolution UI sprites.
    /// </param>
    public static new Bitmap Load(string filePath, bool mipmaps = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var bytes = File.ReadAllBytes(filePath);
        return Decode(bytes, mipmaps);
    }

    /// <summary>
    /// Decodes an image from an encoded byte span (PNG, JPG, WebP, ...)
    /// using SkiaSharp.
    /// </summary>
    public static new Bitmap Decode(ReadOnlySpan<byte> bytes, bool mipmaps = false)
    {
        // Always allocate ABGR8888 — the GPU fast-path sampling
        // format. CopyFromBitmap converts per pixel, so the bitmap's
        // native color type (Skia's default Bgra8888 on little-endian)
        // is normalized here and never reaches the GPU upload.
        using var skBitmap = SkiaSharp.SKBitmap.Decode(bytes)
            ?? throw new InvalidOperationException("SkiaSharp could not decode the supplied image bytes.");
        var image = Bitmap.Create(skBitmap.Width, skBitmap.Height, PixelFormat.ABGR8888, mipmaps);
        image.CopyFromBitmap(skBitmap);
        return image;
    }

    /// <summary>
    /// Saves the image to disk. The encoder is selected by file
    /// extension and handled by SkiaSharp: <c>.png</c>,
    /// <c>.jpg</c> / <c>.jpeg</c>, <c>.webp</c>, and <c>.bmp</c>.
    /// </summary>
    /// <param name="filename">Destination path. Extension picks the format.</param>
    /// <param name="quality">
    /// Encoder quality 0..100 for lossy formats (JPEG, WebP). Ignored
    /// for lossless formats (BMP, PNG). Defaults to 90.
    /// </param>
    public void Save(string filename, int quality = 90)
    {
        ArgumentException.ThrowIfNullOrEmpty(filename);
        if (IsDisposed)
            return;

        var ext = Path.GetExtension(filename);
        var format = ext.ToLowerInvariant() switch
        {
            ".png" => SkiaSharp.SKEncodedImageFormat.Png,
            ".jpg" or ".jpeg" => SkiaSharp.SKEncodedImageFormat.Jpeg,
            ".webp" => SkiaSharp.SKEncodedImageFormat.Webp,
            ".bmp" => SkiaSharp.SKEncodedImageFormat.Bmp,
            _ => throw new NotSupportedException(
                $"Unsupported image format '{ext}'. Supported: .png, .jpg, .jpeg, .webp, .bmp."),
        };

        // Round-trip through an SKBitmap snapshot. The pixel-by-pixel
        // copy is the cost of converting between Blitter's surface and
        // SkiaSharp's bitmap representations; for one-shot saves this
        // is fine, and it keeps the in-memory Bitmap untouched.
        using var bitmap = this.ToSKBitmap();
        using var stream = File.Create(filename);
        if (!bitmap.Encode(stream, format, quality))
            throw new InvalidOperationException(
                $"SkiaSharp failed to encode image as {format}.");
    }

    /// <summary>
    /// Surface flags. Used internally to inspect surface state.
    /// </summary>
    internal SDL.SurfaceFlags Flags
    {
        get
        {
            if (IsDisposed)
                return default;
            unsafe
            {
                SDL.Surface* surface = (SDL.Surface*)_imageId;
                return surface->Flags;
            }
        }
    }

    #region Properties

    private bool _hasTransparentColor; // defaults to false

    /// <summary>
    /// The color that is treated as transparent on the surface.
    /// </summary>
    public Color? TransparentColor
    {
        get
        {
            if (IsDisposed || !_hasTransparentColor)
                return null;
            SDL.GetSurfaceColorKey(_imageId, out var key);
            return MapToColor(key);
        }
        set
        {
            if (IsDisposed)
                return;

            if (value is Color color)
            {
                var colorKey = MapToPixel(color);
                SDL.SetSurfaceColorKey(_imageId, true, colorKey);
                _hasTransparentColor = true;
            }
            else
            {
                SDL.SetSurfaceColorKey(_imageId, false, 0);
            }
            unchecked { _version++; }
        }
    }

    private Palette? _palette;

    /// <summary>
    /// The color palette for this surface, if any.
    /// </summary>
    public Palette Palette
    {
        get
        {
            if (_palette == null)
            {
                var paletteId = SDL.GetSurfacePalette(_imageId);
                _palette = paletteId == 0 ? Palette.Empty : new Palette(paletteId);
            }

            return _palette;
        }
    }

    public int Pitch
    {
        get
        {
            if (IsDisposed)
                return default;
            unsafe
            {
                SDL.Surface* surface = (SDL.Surface*)_imageId;
                return surface->Pitch;
            }
        }
    }

    public override PixelFormat PixelFormat
    {
        get
        {
            if (IsDisposed)
                return default;
            unsafe
            {
                SDL.Surface* surface = (SDL.Surface*)_imageId;
                return (PixelFormat)surface->Format;
            }
        }
    }

    public PixelFormatDetails PixelFormatDetails
    {
        get
        {
            if (IsDisposed)
                return default;
            return PixelFormatDetails.From(SdlPixelFormatDetails);
        }
    }

    private SDL.PixelFormatDetails SdlPixelFormatDetails
    {
        get
        {
            unsafe
            {
                SDL.Surface* surface = (SDL.Surface*)_imageId;
                return *(SDL.PixelFormatDetails*)SDL.GetPixelFormatDetails(surface->Format);
            }
        }
    }

    public int BytesPerPixel
    {
        get
        {
            if (IsDisposed)
                return default;
            unsafe
            {
                SDL.Surface* surface = (SDL.Surface*)_imageId;
                return (int)SDL.BytesPerPixel(surface->Format);
            }
        }
    }

    #endregion

    /// <summary>
    /// Returns a view over the raw pixel bytes of the surface (Height * Pitch bytes).
    /// The returned span aliases the surface's internal buffer; do not retain it
    /// after the surface is disposed.
    /// </summary>
    public unsafe ReadOnlySpan<byte> GetPixels()
    {
        ThrowIfDisposed();
        SDL.Surface* s = (SDL.Surface*)_imageId;
        return new ReadOnlySpan<byte>((void*)s->Pixels, s->Height * s->Pitch);
    }

    /// <summary>
    /// Internal writable view over the surface's pixel bytes, used by
    /// renderers that copy GPU-side results back into the image. Callers
    /// are responsible for invalidating the image after writing.
    /// </summary>
    internal unsafe Span<byte> WritablePixels
    {
        get
        {
            ThrowIfDisposed();
            SDL.Surface* s = (SDL.Surface*)_imageId;
            return new Span<byte>((void*)s->Pixels, s->Height * s->Pitch);
        }
    }

    /// <summary>
    /// Gets the color of the pixel at (x, y).
    /// </summary>
    public Color GetPixel(int x, int y)
    {
        if (IsDisposed)
            return default;

        var bpp = this.BytesPerPixel;
        var formatDetails = this.PixelFormatDetails;

        unsafe
        {
            // Get surface info
            SDL.Surface* s = (SDL.Surface*)_imageId;

            byte* pixels = (byte*)s->Pixels;
            int pitch = s->Pitch;
            byte* pixel = pixels + y * pitch + x * bpp;

            uint pixelValue = 0;
            switch (bpp)
            {
                case 1:
                    pixelValue = *pixel;
                    break;
                case 2:
                    pixelValue = *(ushort*)pixel;
                    break;
                case 3:
                    // 3 bytes: assemble manually (little-endian)
                    pixelValue = (uint)(pixel[0] | (pixel[1] << 8) | (pixel[2] << 16));
                    break;
                case 4:
                    pixelValue = *(uint*)pixel;
                    break;
            }

            return MapToColor(pixelValue);
        }
    }

    /// <summary>
    /// Sets the color of the pixel at (x, y).
    /// </summary>
    public void SetPixel(int x, int y, Color color)
    {
        if (IsDisposed)
            return;
        var bpp = this.BytesPerPixel;
        var pixelValue = MapToPixel(color);
        unsafe
        {
            // Get surface info
            SDL.Surface* s = (SDL.Surface*)_imageId;
            byte* pixels = (byte*)s->Pixels;
            int pitch = s->Pitch;
            byte* pixel = pixels + y * pitch + x * bpp;

            switch(bpp)
            {
                case 1:
                    *pixel = (byte)pixelValue;
                    break;
                case 2:
                    *(ushort*)pixel = (ushort)pixelValue;
                    break;
                case 3:
                    // Write 3 bytes manually (little-endian)
                    pixel[0] = (byte)(pixelValue & 0xFF);
                    pixel[1] = (byte)((pixelValue >> 8) & 0xFF);
                    pixel[2] = (byte)((pixelValue >> 16) & 0xFF);
                    break;
                case 4:
                    *(uint*)pixel = pixelValue;
                    break;
                }
        }
        unchecked { _version++; }
    }

    /// <summary>
    /// Convert surface pixel value to Color
    /// </summary>
    private Color MapToColor(uint pixel)
    {
        if (IsDisposed)
            return default;
        var formatDetails = SdlPixelFormatDetails;
        SDL.GetRGBA(pixel, formatDetails, this.Palette?.Id ?? 0, out byte r, out byte g, out byte b, out byte a);
        return new Color(r, g, b, a);
    }

    /// <summary>
    /// Convert Color to surface pixel value
    /// </summary>
    private uint MapToPixel(Color color)
    {
        if (IsDisposed)
            return default;
        unsafe
        {
            var formatDetails = SdlPixelFormatDetails;
            SDL.PixelFormatDetails* formatDetailsPtr = &formatDetails;
            var paletteId = this.Palette?.Id ?? 0;
            return SDL.MapRGBA((nint)formatDetailsPtr, paletteId, color.R, color.G, color.B, color.A);
        }
    }

    /// <summary>
    /// Replaces every pixel whose color is within <paramref name="tolerance"/>
    /// of <paramref name="oldColor"/> with <paramref name="newColor"/>.
    /// </summary>
    public void ReplaceMatchingColor(Color oldColor, Color newColor, int tolerance = Color.DefaultColorTolerance)
    {
        TransformPixels(context =>
        {
            if (context.Color.IsClosedTo(oldColor, tolerance))
                context.Color = newColor;
        });
    }

    /// <summary>
    /// Sets the alpha channel of every pixel whose color is within
    /// <paramref name="tolerance"/> of <paramref name="color"/>.
    /// </summary>
    public void SetAlpha(byte alpha, Color color, int tolerance = Color.DefaultColorTolerance)
    {
        TransformPixels(context =>
        {
            if (context.Color.IsClosedTo(color, tolerance))
                context.Color = context.Color.WithAlpha(alpha);
        });
    }

    /// <summary>
    /// Invokes <paramref name="action"/> for every pixel in the image.
    /// </summary>
    public void TransformPixels(Action<PixelContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var (width, height) = Size;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                action(new PixelContext(this, x, y));
            }
        }
    }

    /// <summary>
    /// Returns a new image with the pixels flipped along the requested
    /// axis. The source image is unchanged. The new image inherits the
    /// source's <see cref="PixelFormat"/> and <see cref="Mipmaps"/>
    /// flag. <see cref="FlipMode.None"/> still allocates a fresh copy.
    /// </summary>
    public Bitmap Flip(FlipMode mode)
    {
        ThrowIfDisposed();
        var (width, height) = Size;
        var result = Bitmap.Create(width, height, PixelFormat, Mipmaps);
        // Pixel-by-pixel via GetPixel/SetPixel so this works for every
        // PixelFormat we support (including indexed and packed formats)
        // without per-format byte arithmetic. Future optimisation: a
        // per-format byte-stride memcpy path for Horizontal+Vertical
        // on the common 32-bit RGBA formats.
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int srcX = mode == FlipMode.Horizontal ? width - 1 - x : x;
                int srcY = mode == FlipMode.Vertical ? height - 1 - y : y;
                result.SetPixel(x, y, GetPixel(srcX, srcY));
            }
        }
        return result;
    }

    /// <summary>
    /// Returns a new image rotated by the requested right angle. The
    /// source image is unchanged. For <see cref="Rotation.Clockwise90"/>
    /// and <see cref="Rotation.Counterclockwise90"/> the result's
    /// width and height are swapped. The new image inherits the
    /// source's <see cref="PixelFormat"/> and <see cref="Mipmaps"/>
    /// flag. <see cref="Rotation.None"/> still allocates a fresh copy.
    /// </summary>
    public Bitmap Rotate(Rotation rotation)
    {
        ThrowIfDisposed();
        var (width, height) = Size;
        var (resultWidth, resultHeight) = rotation switch
        {
            Rotation.Clockwise90 or Rotation.Counterclockwise90 => (height, width),
            _ => (width, height),
        };
        var result = Bitmap.Create(resultWidth, resultHeight, PixelFormat, Mipmaps);
        // Pixel-by-pixel via GetPixel/SetPixel for the same reasons as
        // Flip. Future optimisation: row-stride memcpy for Half on the
        // common 32-bit formats; transposed strided copy for the 90s.
        for (int y = 0; y < resultHeight; y++)
        {
            for (int x = 0; x < resultWidth; x++)
            {
                var (srcX, srcY) = rotation switch
                {
                    Rotation.Clockwise90 => (y, resultWidth - 1 - x),
                    Rotation.Counterclockwise90 => (resultHeight - 1 - y, x),
                    Rotation.Half => (width - 1 - x, height - 1 - y),
                    _ => (x, y),
                };
                result.SetPixel(x, y, GetPixel(srcX, srcY));
            }
        }
        return result;
    }

    /// <summary>
    /// A 2D renderer that draws into an <see cref="Bitmap"/> in CPU memory using
    /// SDL's software renderer. Pixels written by this renderer land directly
    /// in the image's surface.
    /// </summary>
    private sealed class Renderer : BitmapRenderer2D
    {
        private readonly Bitmap _image;

        private Renderer(Bitmap image, nint rendererId)
            : base(rendererId)
        {
            _image = image;
        }

        /// <summary>
        /// Creates a software renderer that draws into <paramref name="image"/>.
        /// </summary>
        public static Renderer Create(Bitmap image)
        {
            ArgumentNullException.ThrowIfNull(image);
            image.ThrowIfDisposed();

            _ = Application.Current;
            SDL.InitSubSystem(SDL.InitFlags.Video);

            var rendererId = SDL.CreateSoftwareRenderer(image._imageId);
            if (rendererId == 0)
                throw new InvalidOperationException(
                    $"Failed to create software renderer for image: {SDL.GetError()}");

            return new Renderer(image, rendererId);
        }

        /// <summary>The <see cref="Blitter.Bitmap"/> this renderer draws into.</summary>
        public Bitmap Bitmap => _image;

        protected override void OnDisposed()
        {
            // Pixels were written through SDL's renderer rather than the
            // version-tracked SetPixel path, so any cached GPU upload of this
            // image needs to re-stage on next use.
            if (!_image.IsDisposed)
                _image.Invalidate();
        }
    }
}

/// <summary>
/// Mutable cursor over a single pixel of an <see cref="Bitmap"/>, used by
/// <see cref="Bitmap.TransformPixels"/>.
/// </summary>
public struct PixelContext
{
    private readonly Bitmap _surface;

    public int X { get; }
    public int Y { get; }

    internal PixelContext(Bitmap surface, int x, int y)
    {
        _surface = surface;
        X = x;
        Y = y;
    }

    private Color? _color;

    public Color Color
    {
        get
        {
            _color ??= _surface.GetPixel(X, Y);
            return _color.Value;
        }
        set
        {
            _color = value;
            _surface.SetPixel(X, Y, value);
        }
    }
}


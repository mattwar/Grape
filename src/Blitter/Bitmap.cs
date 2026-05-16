using SkiaSharp;

namespace Blitter;

/// <summary>
/// Represents an image bitmap in memory.
/// </summary>
public sealed class Bitmap : Texture2D
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
    /// Creates a new (CPU-side) bitmap.
    /// </summary>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    /// <param name="format">The pixel format of the image.</param>
    /// <param name="mipmaps">Hint to GPU to auto-create mipmaps for the image.</param>
    public static Bitmap Create(int width, int height, PixelFormat format = PixelFormat.ABGR8888, bool mipmaps = false)
    {
        var imageId = SDL.CreateSurface(width, height, (SDL.PixelFormat)format);
        if (imageId == 0)
            throw new InvalidOperationException("Cannot create image");
        return new Bitmap(imageId, mipmaps);
    }

    /// <inheritdoc/>
    public override bool IsDisposed => _imageId == 0;

    /// <summary>
    /// The version number, bumped whenever the bitmap is changed.
    /// </summary>
    public override int Version => _version;

    /// <summary>
    /// Marks the image contents as changed.
    /// Use this to force a re-upload to the GPU.
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
    /// Render into this <see cref="Bitmap"/> using a 2D renderer.
    /// </summary>
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
    /// Render into this image using a 2D renderer.
    /// The image is filled with the background color before the action is invoked.
    /// </summary>
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
    /// Render into this image using a 3D renderer
    /// </summary>
    public void Render3D(Action<Renderer3D> renderAction)
        => Render3DCore(null, renderAction);

    /// <summary>
    /// Render into this image using a 3D renderer.
    /// The bitmap is filled with the background color before the action is invoked.
    /// </summary>
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
    /// Loads an image from disk.
    /// </summary>
    /// <param name="filePath">Path to the image file.</param>
    /// <param name="mipmaps">Hint to GPU to auto-create mipmaps for the image.</param>
    public static Bitmap Load(string filePath, bool mipmaps = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var bytes = File.ReadAllBytes(filePath);
        return Decode(bytes, mipmaps);
    }

    /// <summary>
    /// Loads an image from a stream.
    /// </summary>
    /// <param name="stream">Source stream. Not closed by this call.</param>
    /// <param name="mipmaps">Hint to GPU to auto-create mipmaps for the image.</param>
    public static Bitmap Load(Stream stream, bool mipmaps = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var skBitmap = SkiaSharp.SKBitmap.Decode(stream)
            ?? throw new InvalidOperationException("SkiaSharp could not decode the supplied image stream.");
        var image = Bitmap.Create(skBitmap.Width, skBitmap.Height, PixelFormat.ABGR8888, mipmaps);
        image.CopyFromBitmap(skBitmap);
        return image;
    }

    /// <summary>
    /// Decodes an image from an encoded byte span (PNG, JPG, WebP, ...)
    /// </summary>
    /// <param name="bytes">The encoded image bytes.</param>
    /// <param name="mipmaps">Hint to GPU to auto-create mipmaps for the image.</param>
    public static Bitmap Decode(ReadOnlySpan<byte> bytes, bool mipmaps = false)
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
    /// Saves the image to the stream in the given format.
    /// </summary>
    /// <param name="stream">Destination stream. Not closed by this call.</param>
    /// <param name="format">Target image format (e.g. <c>"png"</c>, <c>"jpg"</c>).</param>
    /// <param name="quality">Encoder quality (0..100), used by lossy formats.</param>
    public void Save(Stream stream, string format, int quality = 90)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrEmpty(format);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        // SkiaSharp's distributed Skia build disables the BMP encoder,
        // so SKBitmap.Encode(..., Bmp) returns false. Route BMP through
        // SDL's native SDL_SaveBMPIO instead, which is built in.
        if (format.ToLowerInvariant().TrimStart('.') == "bmp")
        {
            SaveBmpToStream(stream);
            return;
        }

        var skFormat = ParseEncodedImageFormat(format);

        // Round-trip through an SKBitmap snapshot. The pixel-by-pixel
        // copy is the cost of converting between Blitter's surface and
        // SkiaSharp's bitmap representations; for one-shot saves this
        // is fine, and it keeps the in-memory Bitmap untouched.
        using var bitmap = this.ToSKBitmap();
        if (!bitmap.Encode(stream, skFormat, quality))
            throw new InvalidOperationException(
                $"SkiaSharp failed to encode image as {skFormat}.");
    }

    private void SaveBmpToStream(Stream stream)
    {
        // Encode into an SDL dynamic-memory IOStream, then copy the
        // grown buffer into the caller's stream. SDL owns / frees the
        // buffer when CloseIO is called.
        var io = SDL.IOFromDynamicMem();
        if (io == 0)
            throw new InvalidOperationException(
                $"SDL_IOFromDynamicMem failed: {SDL.GetError()}");
        try
        {
            if (!SDL.SaveBMPIO(_imageId, io, false))
                throw new InvalidOperationException(
                    $"SDL_SaveBMP_IO failed: {SDL.GetError()}");

            var size = SDL.TellIO(io);
            if (size < 0)
                throw new InvalidOperationException(
                    $"SDL_TellIO failed: {SDL.GetError()}");

            var props = SDL.GetIOProperties(io);
            var bufferPtr = SDL.GetPointerProperty(
                props, SDL.Props.IOStreamDynamicMemoryPointer, 0);
            if (bufferPtr == 0)
                throw new InvalidOperationException(
                    "SDL dynamic-memory IOStream did not expose its buffer.");

            unsafe
            {
                var span = new ReadOnlySpan<byte>((void*)bufferPtr, (int)size);
                stream.Write(span);
            }
        }
        finally
        {
            SDL.CloseIO(io);
        }
    }

    /// <summary>
    /// Saves the image to a file.
    /// The format is determiend by the file extension.
    /// </summary>
    public void Save(string filename, int quality = 90)
    {
        ArgumentException.ThrowIfNullOrEmpty(filename);
        if (IsDisposed)
            return;

        using var stream = File.Create(filename);
        Save(stream, Path.GetExtension(filename), quality);
    }

    // Accepts "png", ".png", "PNG", etc. Shared by both Save overloads.
    private static SkiaSharp.SKEncodedImageFormat ParseEncodedImageFormat(string format) =>
        format.ToLowerInvariant().TrimStart('.') switch
        {
            "png" => SkiaSharp.SKEncodedImageFormat.Png,
            "jpg" or "jpeg" => SkiaSharp.SKEncodedImageFormat.Jpeg,
            "webp" => SkiaSharp.SKEncodedImageFormat.Webp,
            "bmp" => SkiaSharp.SKEncodedImageFormat.Bmp,
            _ => throw new NotSupportedException(
                $"Unsupported image format '{format}'. Supported: png, jpg, jpeg, webp, bmp."),
        };

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
    /// The color that is treated as transparent.
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

    /// <summary>
    /// The number of bytes between the start of each pixel row in memory.
    /// </summary>
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

    /// <summary>
    /// The pixel format of the bitmap
    /// </summary>
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

    /// <summary>
    /// Detailed information about the pixel format.
    /// </summary>
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

    /// <summary>
    /// The number of bytes used to represent a single pixel.
    /// </summary>
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
    /// The raw bytes of the bitmap pixels.
    /// </summary>
    public unsafe ReadOnlySpan<byte> GetPixels()
    {
        ThrowIfDisposed();
        SDL.Surface* s = (SDL.Surface*)_imageId;
        return new ReadOnlySpan<byte>((void*)s->Pixels, s->Height * s->Pitch);
    }

    /// <summary>
    /// The raw bytes of the bitmap pixels, writable.
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
    /// Returns a new bitmap with the pixels flipped along the requested axis.
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
    /// Returns a new bitmap rotated by the requested right angle.
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


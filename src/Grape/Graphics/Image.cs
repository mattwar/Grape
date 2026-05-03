namespace Grape;

/// <summary>
/// Represents an image bitmap in memory.
/// </summary>
public sealed class Image : IDisposable
{
    private readonly Application _application;
    internal nint _imageId;
    private int _version;

    private Image(nint imageId)
    {
        _imageId = imageId;
        _application = Application.Current;
        _application.AddResource(this);
        _version = 1;
    }

    public static Image Create(int width, int height, PixelFormat format)
    {
        // SDL must be initialised before any SDL.* call. Touching
        // Application.Current starts the app on demand.
        _ = Application.Current;

        var imageId = SDL.CreateSurface(width, height, (SDL.PixelFormat)format);
        if (imageId == 0)
            throw new InvalidOperationException("Cannot create image");
        return new Image(imageId);
    }

    public bool IsDisposed => _imageId == 0;

    /// <summary>
    /// Bumped each time the image's contents change. Renderers use this to
    /// detect when their cached GPU texture upload is stale. If you mutate
    /// the image through raw pixel access (e.g. <see cref="GetPixels"/> or
    /// an external software renderer), call <see cref="Invalidate"/> to mark
    /// the change.
    /// </summary>
    public int Version => _version;

    /// <summary>
    /// Marks the image contents as changed so renderers re-upload their
    /// cached GPU texture on the next draw. Mutations through
    /// <see cref="SetPixel"/> or <see cref="TransparentColor"/> bump the
    /// version automatically; this is for callers that touch raw pixels.
    /// </summary>
    public void Invalidate()
    {
        if (IsDisposed)
            return;
        unchecked { _version++; }
    }

    internal void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(Image));
    }

    /// <summary>
    /// Renders into this image using a software 2D renderer.
    /// </summary>
    public void RenderImage(Action<Renderer2D> renderAction)
    {
        ArgumentNullException.ThrowIfNull(renderAction);
        ThrowIfDisposed();
        using var renderer = ImageRenderer2D.Create(this);
        renderAction(renderer);
        renderer.Present();
        Invalidate();
    }

    public void Dispose()
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
    /// Loads a bitmap from the specified file path.
    /// </summary>
    public static Image LoadBitmap(string filePath)
    {
        _ = Application.Current;

        var imageId = SDL.LoadBMP(filePath);
        if (imageId == 0)
            throw new InvalidOperationException($"SDL_LoadBMP Error: {SDL.GetError()}");
        return new Image(imageId);
    }

    /// <summary>
    /// Save the contents of the image to a BMP file.
    /// </summary>
    public void Save(string filename)
    {
        if (IsDisposed)
            return;
        SDL.SaveBMP(_imageId, filename);
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

    public PixelFormat PixelFormat
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

    /// <summary>
    /// The size of the surface in pixels.
    /// </summary>
    public (int Width, int Height) Size
    {
        get
        {
            if (IsDisposed)
                return default;
            unsafe
            {
                SDL.Surface* surface = (SDL.Surface*)_imageId;
                return (surface->Width, surface->Height);
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
}

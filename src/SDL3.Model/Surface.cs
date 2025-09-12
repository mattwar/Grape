namespace SDL3.Model;

/// <summary>
/// Represents a bitmap
/// </summary>
public sealed class Surface : IDisposable
{
    private readonly Application _application;
    internal nint _surfaceId;

    private Surface(nint surfaceId)
    {
        _surfaceId = surfaceId;
        _application = Application.Current ?? throw new InvalidOperationException("No current application");
        _application.AddResource(this);
    }

    public static Surface Create(int width, int height, SDL.PixelFormat format)
    {
        var surfaceId = SDL.CreateSurface(width, height, format);
        if (surfaceId == 0)
            throw new InvalidOperationException("Cannot create surface");
        return new Surface(surfaceId);
    }

    public bool IsDisposed => _surfaceId == 0;

    internal void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException("Surface");
    }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            var id = Interlocked.Exchange(ref _surfaceId, 0);
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
    public static Surface LoadBitmap(string filePath)
    {
        var surfaceId = SDL.LoadBMP(filePath);
        if (surfaceId == 0)
            throw new InvalidOperationException($"SDL_LoadBMP Error: {SDL.GetError()}");
        return new Surface(surfaceId);
    }

    /// <summary>
    /// Save the contents of the surface to a BMP file.
    /// </summary>
    public void Save(string filename)
    {
        if (IsDisposed)
            return;
        SDL.SaveBMP(_surfaceId, filename);
    }

    /// <summary>
    /// Any flags used when creating the surface.
    /// </summary>
    public SDL.SurfaceFlags Flags
    {
        get
        {
            if (IsDisposed)
                return default;
            unsafe
            {
                SDL.Surface* surface = (SDL.Surface*)_surfaceId;
                return surface->Flags;
            }
        }
    }

    #region Properties


    private bool _hasTransparentColor; // defaults to false

    /// <summary>
    /// The color that is treated as transparent on the surface.
    /// </summary>
    public SDL.Color? TransparentColor
    {
        get
        {
            if (IsDisposed || !_hasTransparentColor)
                return null;
            SDL.GetSurfaceColorKey(_surfaceId, out var key);
            return MapToColor(key);
        }
        set
        {
            if (IsDisposed)
                return;

            if (value is SDL.Color color)
            {
                var colorKey = MapToPixel(color);
                SDL.SetSurfaceColorKey(_surfaceId, true, colorKey);
                _hasTransparentColor = true;
            }
            else
            {
                SDL.SetSurfaceColorKey(_surfaceId, false, 0);
            }
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
                var paletteId = SDL.GetSurfacePalette(_surfaceId);
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
                SDL.Surface* surface = (SDL.Surface*)_surfaceId;
                return surface->Pitch;
            }
        }
    }

    public SDL.PixelFormat PixelFormat
    {
        get
        {
            if (IsDisposed)
                return default;
            unsafe
            {
                SDL.Surface* surface = (SDL.Surface*)_surfaceId;
                return surface->Format;
            }
        }
    }

    public SDL.PixelFormatDetails PixelFormatDetails
    {
        get
        {
            if (IsDisposed)
                return default;
            unsafe
            {
                SDL.Surface* surface = (SDL.Surface*)_surfaceId;
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
                SDL.Surface* surface = (SDL.Surface*)_surfaceId;
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
                SDL.Surface* surface = (SDL.Surface*)_surfaceId;
                return (surface->Width, surface->Height);
            }
        }
    }
    #endregion

    /// <summary>
    /// Gets the color of the pixel at (x, y).
    /// </summary>
    public SDL.Color GetPixel(int x, int y)
    {
        if (IsDisposed)
            return default;

        var bpp = this.BytesPerPixel;
        var formatDetails = this.PixelFormatDetails;

        unsafe
        {
            // Get surface info
            SDL.Surface* s = (SDL.Surface*)_surfaceId;

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
    public void SetPixel(int x, int y, SDL.Color color)
    {
        if (IsDisposed)
            return;
        var bpp = this.BytesPerPixel;
        var pixelValue = MapToPixel(color);
        unsafe
        {
            // Get surface info
            SDL.Surface* s = (SDL.Surface*)_surfaceId;
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
    }

    /// <summary>
    /// Convert surface pixel value to SDL.Color
    /// </summary>
    private SDL.Color MapToColor(uint pixel)
    {
        if (IsDisposed)
            return default;
        var formatDetails = this.PixelFormatDetails;
        SDL.GetRGBA(pixel, formatDetails, this.Palette?.Id ?? 0, out byte r, out byte g, out byte b, out byte a);
        return new SDL.Color { R = r, G = g, B = b, A = a };
    }

    /// <summary>
    /// Convert SDL.Color to surface pixel value
    /// </summary>
    private uint MapToPixel(SDL.Color color)
    {
        if (IsDisposed)
            return default;
        unsafe
        {
            var formatDetails = this.PixelFormatDetails;
            SDL.PixelFormatDetails* formatDetailsPtr = &formatDetails;
            var paletteId = this.Palette?.Id ?? 0;
            return SDL.MapRGBA((nint)formatDetailsPtr, paletteId, color.R, color.G, color.B, color.A);
        }
    }
}
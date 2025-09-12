namespace SDL3.Model;


/// <summary>
/// A bitmap stored on the GPU that can be drawn.
/// </summary>
public sealed class Texture : IDisposable
{
    private readonly Renderer _renderer;
    private nint _textureId;

    internal Texture(Renderer renderer, nint surfaceId)
    {
        _renderer = renderer;
        _textureId = surfaceId;
        renderer.AddResource(this);
    }

    /// <summary>
    /// The <see cref="Renderer"/> that created this <see cref="Texture"/>.
    /// </summary>
    public Renderer Renderer => _renderer;

    internal nint Id => _textureId;

    public bool IsDisposed => _textureId == 0;

    public void Dispose()
    {
        if (!IsDisposed)
        {
            var id = Interlocked.Exchange(ref _textureId, 0);
            if (id != 0)
            {
                SDL.DestroySurface(id);
                _renderer.RemoveResource(this);
            }
        }
    }

    #region Properties

    public byte AlphaMod
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetTextureAlphaMod(_textureId, out var alpha);
            return alpha;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetTextureAlphaMod(_textureId, value);
        }
    }

    public float AlphaModFloat
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetTextureAlphaModFloat(_textureId, out var alpha);
            return alpha;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetTextureAlphaModFloat(_textureId, value);
        }
    }

    public SDL.BlendMode BlendMode
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetTextureBlendMode(_textureId, out var mode);
            return mode;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetTextureBlendMode(_textureId, value);
        }
    }

    public (byte R, byte G, byte B) ColorMod
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetTextureColorMod(_textureId, out var r, out var g, out var b);
            return (r, g, b);
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetTextureColorMod(_textureId, value.R, value.G, value.B);
        }
    }

    public (float R, float G, float B) ColorModFloat
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetTextureColorModFloat(_textureId, out var r, out var g, out var b);
            return (r, g, b);
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetTextureColorModFloat(_textureId, value.R, value.G, value.B);
        }
    }

    public SDL.ScaleMode ScaleMode
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetTextureScaleMode(_textureId, out var mode);
            return mode;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetTextureScaleMode(_textureId, value);
        }
    }

    /// <summary>
    /// The size of the texture in pixels.
    /// </summary>
    public (float Width, float Height) Size
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetTextureSize(_textureId, out var width, out var height);
            return (width, height);
        }
    }

    #endregion
}

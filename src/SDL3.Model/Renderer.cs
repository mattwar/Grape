using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SDL3.Model;

public sealed class Renderer : IDisposable
{
    private readonly Window _window;
    private readonly string _name;
    private nint _rendererId;

    internal Renderer(Window window, nint rendererId, string? name)
    {
        _window = window;
        _name = name ?? "";
        _rendererId = rendererId;
    }

    private ImmutableList<IDisposable> _resources = ImmutableList<IDisposable>.Empty;

    internal void AddResource(IDisposable resource)
    {
        ImmutableInterlocked.Update(ref _resources, list => list.Add(resource));
    }

    internal void RemoveResource(IDisposable resource)
    {
        ImmutableInterlocked.Update(ref _resources, list => list.Remove(resource));
    }

    /// <summary>
    /// True if this <see cref="Renderer"/> has been disposed.
    /// </summary>
    public bool IsDisposed => _rendererId == 0;

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(_name);
    }

    /// <summary>
    /// Disposes this <see cref="Renderer"/>, releasing its resources.
    /// </summary>
    public void Dispose()
    {
        if (_rendererId != 0)
        {
            var id = Interlocked.Exchange(ref _rendererId, 0);
            if (id != 0)
            {
                foreach (var resource in _resources)
                {
                    resource.Dispose();
                }

                SDL.DestroyRenderer(id);
                _window.RemoveResource(this);
            }
        }
    }

    #region Properties

    /// <summary>
    /// The <see cref="Window"/> this <see cref="Renderer"/> renders to.
    /// </summary>
    public Window Window => _window;

    /// <summary>
    /// The current blend mode used for drawing operations.
    /// </summary>
    public SDL.BlendMode BlendMode
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetRenderDrawBlendMode(_rendererId, out var mode);
            return mode;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetRenderDrawBlendMode(_rendererId, value);
        }
    }

    /// <summary>
    /// Gets or sets the clipping rectangle for the renderer.
    /// </summary>
    /// <remarks>The clipping rectangle restricts rendering to the specified area. Any drawing operations
    /// outside this rectangle will be ignored. To disable clipping, set the rectangle to <see langword="null"/> or an
    /// empty rectangle.</remarks>
    public SDL.Rect ClipRect
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetRenderClipRect(_rendererId, out var rect);
            return rect;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetRenderClipRect(_rendererId, value);
        }
    }

    /// <summary>
    /// The current scaling factor used for drawing operations.
    /// </summary>
    public float ColorScale
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetRenderColorScale(_rendererId, out var scale);
            return scale;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetRenderColorScale(_rendererId, value);
        }
    }

    /// <summary>
    /// The default scale mode used for new textures.
    /// </summary>
    public SDL.ScaleMode DefaultScaleMode
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetDefaultTextureScaleMode(_rendererId, out var mode);
            return mode;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetDefaultTextureScaleMode(_rendererId, value);
        }
    }

    /// <summary>
    /// The current draw color.
    /// </summary>
    public SDL.Color DrawColor
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetRenderDrawColor(_rendererId, out var r, out var g, out var b, out var a);
            return new SDL.Color { R = r, G = g, B = b, A = a };
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetRenderDrawColor(_rendererId, value.R, value.G, value.B, value.A);
        }
    }

    /// <summary>
    /// The current draw color as floats from 0.0 to 1.0.
    /// </summary>
    public SDL.FColor DrawColorFloat
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetRenderDrawColorFloat(_rendererId, out var r, out var g, out var b, out var a);
            return new SDL.FColor { R = r, G = g, B = b, A = a };
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetRenderDrawColorFloat(_rendererId, value.R, value.G, value.B, value.A);
        }
    }

    /// <summary>
    /// The logical representation 
    /// </summary>
    public (int Width, int Height, SDL.RendererLogicalPresentation Mode) LogicalRepresentation
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetRenderLogicalPresentation(_rendererId, out var width, out var height, out var mode);
            return (width, height, mode);
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetRenderLogicalPresentation(_rendererId, value.Width, value.Height, value.Mode);
        }
    }

    /// <summary>
    /// Gets the logical representation rectangle based on the presentation mode and output size.
    /// </summary>
    public SDL.FRect LogicalRepresentationRect
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetRenderLogicalPresentationRect(_rendererId, out var rect);
            return rect;
        }
    }

    /// <summary>
    /// The name of the renderer.
    /// </summary>
    public string Name => IsDisposed ? "" : SDL.GetRendererName(_rendererId) ?? "";

    /// <summary>
    /// The output size in pixels.
    /// </summary>
    public (int Width, int Height) OutputSize
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetRenderOutputSize(_rendererId, out var width, out var height);
            return (width, height);
        }
    }

    /// <summary>
    /// The rendering scale
    /// </summary>
    public (float ScaleX, float ScaleY) Scale
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetRenderScale(_rendererId, out var scaleX, out var scaleY);
            return (scaleX, scaleY);
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetRenderScale(_rendererId, value.ScaleX, value.ScaleY);
        }
    }

    /// <summary>
    /// The portion of the rendering target where drawing operations are performed.
    /// </summary>
    public SDL.Rect ViewPort
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetRenderViewport(_rendererId, out var rect);
            return rect;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetRenderViewport(_rendererId, value);
        }
    }

    #endregion

    #region Textures
    /// <summary>
    /// Creates a new <see cref="Texture"/> associated with this <see cref="Renderer"/>.
    /// </summary>
    public Texture CreateTexture(int width, int height, SDL.PixelFormat pixelFormat, SDL.TextureAccess access)
    {
        ThrowIfDisposed();
        var id = SDL.CreateTexture(_rendererId, pixelFormat, access, width, height);
        if (id == 0)
            throw new InvalidOperationException($"SDL.CreateTexture failed: {SDL.GetError()}");
        return new Texture(this, id);
    }

    /// <summary>
    /// Creates a <see cref="Texture"/> from a given <see cref="Surface"/>.
    /// </summary>
    public Texture CreateTexture(Surface surface)
    {
        ThrowIfDisposed();
        surface.ThrowIfDisposed();      
        var id = SDL.CreateTextureFromSurface(_rendererId, surface._surfaceId);
        return new Texture(this, id);
    }
    #endregion

    #region Rendering

    /// <summary>
    /// Fills the current rendering target with the <see cref="DrawColor"/>.
    /// </summary>
    public void Clear()
    {
        if (IsDisposed)
            return;
        SDL.RenderClear(_rendererId);
    }

    /// <summary>
    /// Updates the rendering target to display the most recently rendered content.
    /// </summary>
    /// <remarks>This method finalizes the rendering process by presenting the current rendering target to the
    /// screen. Ensure that the renderer is not disposed before calling this method.</remarks>
    /// <exception cref="ObjectDisposedException">Thrown if the renderer has been disposed.</exception>
    public void Present()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(_name);
        SDL.RenderPresent(_rendererId);
    }

    /// <summary>
    /// Draws debug text at the specified coordinates.
    /// </summary>
    public bool RenderDebugText(int x, int y, string text, float scale = 0f)
    {
        if (IsDisposed)
            return false;

        if (scale > 0f)
        {
            var (oldScaleX, oldScaleY) = this.Scale;
            this.Scale = (scale, scale);
            var result = SDL.RenderDebugText(_rendererId, x / scale, y / scale, text);
            this.Scale = (oldScaleX, oldScaleY);
            return result;
        }
        else
        {
            return SDL.RenderDebugText(_rendererId, x, y, text);
        }
    }

    /// <summary>
    /// Renders the portion of the <see cref="Texture"/> the the destination in the window.
    /// </summary>
    public bool RenderTexture(Texture texture, SDL.FRect source, SDL.FRect destination)
    {
        if (IsDisposed)
            return false;
        return SDL.RenderTexture(_rendererId, texture.Id, source, destination);
    }

    /// <summary>
    /// Renders the entire <see cref="Texture"/> the the destination in the window.
    /// </summary>
    public bool RenderTexture(Texture texture, SDL.FRect destination)
    {
        var source = new SDL.FRect { X = 0, Y = 0, W = texture.Size.Width, H = texture.Size.Height};
        return RenderTexture(texture, source, destination);
    }

    /// <summary>
    /// Renders the entire <see cref="Texture"/> to the location.
    /// </summary>
    public bool RenderTexture(Texture texture, float x, float y, float scale = 1.0f)
    {
        var source = new SDL.FRect { X = 0, Y = 0, W = texture.Size.Width, H = texture.Size.Height };
        var dest = new SDL.FRect { X = x, Y = y, W = texture.Size.Width * scale, H = texture.Size.Height * scale };
        return RenderTexture(texture, source, dest);
    }

    /// <summary>
    /// Renders a portion of the <see cref="Texture"/> rotated by the specified angle around the given center point, to the destination location in the window.
    /// </summary>
    public bool RenderTextureRotated(Texture texture, SDL.FRect source, SDL.FRect destination, float angle, SDL.FPoint center, SDL.FlipMode flip = SDL.FlipMode.None)
    {
        if (IsDisposed)
            return false;
        return SDL.RenderTextureRotated(_rendererId, texture.Id, source, destination, angle, center, flip);
    }

    /// <summary>
    /// Renders the entire <see cref="Texture"/> rotated by the specified angle around the given center point, to the destination location in the window.
    /// </summary>
    public bool RenderTextureRotated(Texture texture, SDL.FRect destination, float angle, SDL.FPoint center, SDL.FlipMode flip = SDL.FlipMode.None)
    {
        var source = new SDL.FRect { X = 0, Y = 0, W = texture.Size.Width, H = texture.Size.Height };
        return SDL.RenderTextureRotated(_rendererId, texture.Id, source, destination, angle, center, flip);
    }

    /// <summary>
    /// Renders the entire <see cref="Texture"/> rotated by the specified angle around the given center point, to the destination location in the window.
    /// </summary>
    public bool RenderTextureRotated(Texture texture, float x, float y, float angle, float centerX, float centerY, float scale = 1.0f, SDL.FlipMode flip = SDL.FlipMode.None)
    {
        var source = new SDL.FRect { X = 0, Y = 0, W = texture.Size.Width, H = texture.Size.Height };
        var destination = new SDL.FRect { X = x, Y = y, W = texture.Size.Width * scale, H = texture.Size.Height * scale };
        var center = new SDL.FPoint { X = centerX * scale, Y = centerY * scale };
        return SDL.RenderTextureRotated(_rendererId, texture.Id, source, destination, angle, center, flip);
    }

    private readonly ConditionalWeakTable<Surface, Texture> _surfaceTextureCache = new();

    /// <summary>
    /// Gets the associated <see cref="Texture"/> for the given <see cref="Surface"/>, creating one if necessary.
    /// </summary>
    private bool TryGetOrCreateTexture(Surface surface, [NotNullWhen(true)] out Texture? texture)
    {
        if (!_surfaceTextureCache.TryGetValue(surface, out texture)
            || texture.IsDisposed)
        {
            texture = this.CreateTexture(surface);
            _surfaceTextureCache.AddOrUpdate(surface, texture);
        }
        return texture != null;
    }

    /// <summary>
    /// Renders the portion of the <see cref="Surface"/> to the destination in the window.
    /// </summary>
    public bool RenderSurface(Surface surface, SDL.FRect source, SDL.FRect destination)
    {
        if (IsDisposed)
            return false;

        if (!TryGetOrCreateTexture(surface, out var texture))
            return false;

        return RenderTexture(texture, source, destination);
    }

    /// <summary>
    /// Renders the entire <see cref="Surface"/> to the destination in the window.
    /// </summary>
    public bool RenderSurface(Surface surface, SDL.FRect destination)
    {
        if (IsDisposed)
            return false;

        if (!TryGetOrCreateTexture(surface, out var texture))
            return false;

        return RenderTexture(texture, destination);
    }

    /// <summary>
    /// Renders the entire <see cref="Surface"/> to the location in the window.
    /// </summary>
    public bool RenderSurface(Surface surface, float x, float y, float scale = 1.0f)
    {
        if (IsDisposed)
            return false;

        if (!TryGetOrCreateTexture(surface, out var texture))
            return false;

        return RenderTexture(texture, x, y, scale);
    }

    public bool RenderSurfaceRotated(Surface surface, SDL.FRect source, SDL.FRect destination, float angle, SDL.FPoint center, SDL.FlipMode flip = SDL.FlipMode.None)
    {
        if (IsDisposed)
            return false;
        if (!TryGetOrCreateTexture(surface, out var texture))
            return false;
        return RenderTextureRotated(texture, source, destination, angle, center, flip);
    }

    public bool RenderSurfaceRotated(Surface surface, SDL.FRect destination, float angle, SDL.FPoint center, SDL.FlipMode flip = SDL.FlipMode.None)
    {
        if (IsDisposed)
            return false;
        if (!TryGetOrCreateTexture(surface, out var texture))
            return false;
        return RenderTextureRotated(texture, destination, angle, center, flip);
    }

    public bool RenderSurfaceRotated(Surface surface, float x, float y, float angle, float centerX, float centerY, float scale = 1.0f, SDL.FlipMode flip = SDL.FlipMode.None)
    {
        if (IsDisposed)
            return false;
        if (!TryGetOrCreateTexture(surface, out var texture))
            return false;
        return RenderTextureRotated(texture, x, y, angle, centerX, centerY, scale, flip);
    }

    #endregion
}

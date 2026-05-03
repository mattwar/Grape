using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SDL3.SDL;

namespace Grape;

public sealed class Renderer2D : IDisposable
{
    private readonly Window2D _window;
    private readonly string _name;
    private nint _rendererId;

    internal Renderer2D(Window2D window, nint rendererId, string? name)
    {
        _window = window;
        _name = name ?? "";
        _rendererId = rendererId;
    }

    /// <summary>
    /// Creates a renderer for this window.
    /// The window already has a default renderer created when the window is created.
    /// </summary>
    internal static Renderer2D Create(Window2D window, string? name = null)
    {
        var rendererId = SDL.CreateRenderer(window.WindowId, name);
        return new Renderer2D(window, rendererId, name);
    }

    /// <summary>
    /// Creates a window gpu renderer with the specified shader format.
    /// </summary>
    internal static Renderer2D Create(Window2D window, SDL.GPUShaderFormat format)
    {
        var rendererId = SDL.CreateGPURenderer(window.WindowId, format, out var gpuDeviceId);
        return new Renderer2D(window, rendererId, null);
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
    /// True if this <see cref="Renderer2D"/> has been disposed.
    /// </summary>
    public bool IsDisposed => _rendererId == 0;

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(_name);
    }

    /// <summary>
    /// Disposes this <see cref="Renderer2D"/>, releasing its resources.
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
    /// The <see cref="Window"/> this <see cref="Renderer2D"/> renders to.
    /// </summary>
    public Window Window => _window;

    /// <summary>
    /// The current blend mode used for drawing operations.
    /// </summary>
    internal SDL.BlendMode BlendMode
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
    public Rect ClipRect
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
            SDL.Rect r = value;
            SDL.SetRenderClipRect(_rendererId, r);
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
    internal SDL.ScaleMode DefaultScaleMode
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
    public Color DrawColor
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetRenderDrawColor(_rendererId, out var r, out var g, out var b, out var a);
            return new Color(r, g, b, a);
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
    internal SDL.FColor DrawColorFloat
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
    internal (int Width, int Height, SDL.RendererLogicalPresentation Mode) LogicalRepresentation
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
    public Rect LogicalRepresentationRect
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
    public Rect ViewPort
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
            SDL.Rect r = value;
            SDL.SetRenderViewport(_rendererId, r);
        }
    }

    #endregion

    #region Textures
    /// <summary>
    /// Creates a new <see cref="Texture"/> associated with this <see cref="Renderer2D"/>.
    /// </summary>
    private Texture CreateTexture(int width, int height, SDL.PixelFormat pixelFormat, SDL.TextureAccess access)
    {
        ThrowIfDisposed();
        var id = SDL.CreateTexture(_rendererId, pixelFormat, access, width, height);
        if (id == 0)
            throw new InvalidOperationException($"SDL.CreateTexture failed: {SDL.GetError()}");
        return new Texture(this, id);
    }

    /// <summary>
    /// Creates a <see cref="Texture"/> from a given <see cref="Image"/>.
    /// </summary>
    private Texture CreateTexture(Image image)
    {
        ThrowIfDisposed();
        image.ThrowIfDisposed();      
        var id = SDL.CreateTextureFromSurface(_rendererId, image._imageId);
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
    private bool RenderTexture(Texture texture, SDL.FRect source, SDL.FRect destination)
    {
        if (IsDisposed)
            return false;
        return SDL.RenderTexture(_rendererId, texture.Id, source, destination);
    }

    /// <summary>
    /// Renders the entire <see cref="Texture"/> the the destination in the window.
    /// </summary>
    private bool RenderTexture(Texture texture, SDL.FRect destination)
    {
        var source = new SDL.FRect { X = 0, Y = 0, W = texture.Size.Width, H = texture.Size.Height};
        return RenderTexture(texture, source, destination);
    }

    /// <summary>
    /// Renders the entire <see cref="Texture"/> to the location.
    /// </summary>
    private bool RenderTexture(Texture texture, float x, float y, float scale = 1.0f)
    {
        var source = new SDL.FRect { X = 0, Y = 0, W = texture.Size.Width, H = texture.Size.Height };
        var dest = new SDL.FRect { X = x, Y = y, W = texture.Size.Width * scale, H = texture.Size.Height * scale };
        return RenderTexture(texture, source, dest);
    }

    /// <summary>
    /// Renders a portion of the <see cref="Texture"/> rotated by the specified angle around the given center point, to the destination location in the window.
    /// </summary>
    private bool RenderTextureRotated(Texture texture, SDL.FRect source, SDL.FRect destination, float angle, SDL.FPoint center, SDL.FlipMode flip = SDL.FlipMode.None)
    {
        if (IsDisposed)
            return false;
        return SDL.RenderTextureRotated(_rendererId, texture.Id, source, destination, angle, center, flip);
    }

    /// <summary>
    /// Renders the entire <see cref="Texture"/> rotated by the specified angle around the given center point, to the destination location in the window.
    /// </summary>
    private bool RenderTextureRotated(Texture texture, SDL.FRect destination, float angle, SDL.FPoint center, SDL.FlipMode flip = SDL.FlipMode.None)
    {
        var source = new SDL.FRect { X = 0, Y = 0, W = texture.Size.Width, H = texture.Size.Height };
        return SDL.RenderTextureRotated(_rendererId, texture.Id, source, destination, angle, center, flip);
    }

    /// <summary>
    /// Renders the entire <see cref="Texture"/> rotated by the specified angle around the given center point, to the destination location in the window.
    /// </summary>
    private bool RenderTextureRotated(Texture texture, float x, float y, float angle, float centerX, float centerY, float scale = 1.0f, SDL.FlipMode flip = SDL.FlipMode.None)
    {
        var source = new SDL.FRect { X = 0, Y = 0, W = texture.Size.Width, H = texture.Size.Height };
        var destination = new SDL.FRect { X = x, Y = y, W = texture.Size.Width * scale, H = texture.Size.Height * scale };
        var center = new SDL.FPoint { X = centerX * scale, Y = centerY * scale };
        return SDL.RenderTextureRotated(_rendererId, texture.Id, source, destination, angle, center, flip);
    }

    private readonly ConditionalWeakTable<Image, ImageTextureEntry> _imageTextureCache = new();

    private sealed class ImageTextureEntry
    {
        public Texture Texture { get; set; } = null!;
        public int Version { get; set; }
    }

    /// <summary>
    /// Gets the associated <see cref="Texture"/> for the given <see cref="Image"/>, creating one if necessary.
    /// Re-creates the cached texture when the image's <see cref="Image.Version"/> has changed.
    /// </summary>
    private bool TryGetOrCreateTexture(Image image, [NotNullWhen(true)] out Texture? texture)
    {
        if (!_imageTextureCache.TryGetValue(image, out var entry))
        {
            entry = new ImageTextureEntry
            {
                Texture = this.CreateTexture(image),
                Version = image.Version,
            };
            _imageTextureCache.AddOrUpdate(image, entry);
        }
        else if (entry.Texture.IsDisposed || entry.Version != image.Version)
        {
            entry.Texture.Dispose();
            entry.Texture = this.CreateTexture(image);
            entry.Version = image.Version;
        }

        texture = entry.Texture;
        return texture != null;
    }

    /// <summary>
    /// Renders the portion of the <see cref="Image"/> to the destination in the window.
    /// </summary>
    public bool RenderImage(Image image, Rect source, Rect destination)
    {
        if (IsDisposed)
            return false;

        if (!TryGetOrCreateTexture(image, out var texture))
            return false;

        return RenderTexture(texture, source, destination);
    }

    /// <summary>
    /// Renders the entire <see cref="Image"/> to the destination in the window.
    /// </summary>
    public bool RenderImage(Image image, Rect destination)
    {
        if (IsDisposed)
            return false;

        if (!TryGetOrCreateTexture(image, out var texture))
            return false;

        return RenderTexture(texture, destination);
    }

    /// <summary>
    /// Renders the entire <see cref="Image"/> to the location in the window.
    /// </summary>
    public bool RenderImage(Image image, float x, float y, float scale = 1.0f)
    {
        if (IsDisposed)
            return false;

        if (!TryGetOrCreateTexture(image, out var texture))
            return false;

        return RenderTexture(texture, x, y, scale);
    }

    public bool RenderImageRotated(Image image, Rect source, Rect destination, float angle, Vector2 center, FlipMode flip = FlipMode.None)
    {
        if (IsDisposed)
            return false;
        if (!TryGetOrCreateTexture(image, out var texture))
            return false;
        var sdlCenter = new SDL.FPoint { X = center.X, Y = center.Y };
        return RenderTextureRotated(texture, source, destination, angle, sdlCenter, (SDL.FlipMode)flip);
    }

    public bool RenderImageRotated(Image image, Rect destination, float angle, Vector2 center, FlipMode flip = FlipMode.None)
    {
        if (IsDisposed)
            return false;
        if (!TryGetOrCreateTexture(image, out var texture))
            return false;
        var sdlCenter = new SDL.FPoint { X = center.X, Y = center.Y };
        return RenderTextureRotated(texture, destination, angle, sdlCenter, (SDL.FlipMode)flip);
    }

    public bool RenderImageRotated(Image image, float x, float y, float angle, float centerX, float centerY, float scale = 1.0f, FlipMode flip = FlipMode.None)
    {
        if (IsDisposed)
            return false;
        if (!TryGetOrCreateTexture(image, out var texture))
            return false;
        return RenderTextureRotated(texture, x, y, angle, centerX, centerY, scale, (SDL.FlipMode)flip);
    }

    public bool RenderFillRect(Rect rect)
    {
        SDL.FRect r = rect;
        return SDL.RenderFillRect(_rendererId, r);
    }

    public bool RenderFillRects(Rect[] rects)
    {
        unsafe
        {
            fixed (Rect* p = rects)
                return SDL3Native.SDL_RenderFillRects(_rendererId, p, rects.Length);
        }
    }

    private bool RenderGeometry(SDL.Vertex[] vertices, int[] indices, Texture? texture = null)
    {
        return SDL.RenderGeometry(_rendererId, texture != null ? texture.Id : 0, vertices, vertices.Length, indices, indices.Length);
    }

    internal bool RenderGeometry(SDL.Vertex[] vertices, int[] indices, Image? surface = null)
    {
        var texture = surface == null ? null
            : TryGetOrCreateTexture(surface!, out var txt) ? txt
            : null;

        return RenderGeometry(vertices, indices, texture);
    }

    public bool RenderLine(float x1, float y1, float x2, float y2)
    {
        return SDL.RenderLine(_rendererId, x1, y1, x2, y2);
    }

    public bool RenderLines(Vector2[] points)
    {
        unsafe
        {
            fixed (Vector2* p = points)
                return SDL3Native.SDL_RenderLines(_rendererId, p, points.Length);
        }
    }

    public bool RenderPoint(float x, float y)
    {
        return SDL.RenderPoint(_rendererId, x, y);
    }

    public bool RenderPoints(Vector2[] points)
    {
        unsafe
        {
            fixed (Vector2* p = points)
                return SDL3Native.SDL_RenderPoints(_rendererId, p, points.Length);
        }
    }
    #endregion
}

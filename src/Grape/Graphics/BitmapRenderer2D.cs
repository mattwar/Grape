using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Grape;

/// <summary>
/// Common base for 2D renderers backed by an SDL_Renderer (window swapchain
/// or software-on-surface). Subclasses bind the renderer to a specific
/// target and decide what gets disposed alongside it.
/// </summary>
internal abstract class BitmapRenderer2D : Renderer2D, IDisposable
{
    private nint _rendererId;
    private ImmutableList<IDisposable> _resources = ImmutableList<IDisposable>.Empty;

    private protected BitmapRenderer2D(nint rendererId)
    {
        _rendererId = rendererId;
    }

    internal nint RendererId => _rendererId;

    internal void AddResource(IDisposable resource)
    {
        ImmutableInterlocked.Update(ref _resources, list => list.Add(resource));
    }

    internal void RemoveResource(IDisposable resource)
    {
        ImmutableInterlocked.Update(ref _resources, list => list.Remove(resource));
    }

    /// <summary>True if this renderer has been disposed.</summary>
    public bool IsDisposed => _rendererId == 0;

    private protected void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    /// <summary>Disposes the renderer, releasing its SDL_Renderer and any tracked child resources.</summary>
    public void Dispose()
    {
        if (_rendererId == 0)
            return;

        var id = Interlocked.Exchange(ref _rendererId, 0);
        if (id == 0)
            return;

        foreach (var resource in _resources)
            resource.Dispose();

        SDL.DestroyRenderer(id);
        OnDisposed();
    }

    /// <summary>Hook invoked after the SDL_Renderer has been destroyed.</summary>
    protected virtual void OnDisposed() { }

    #region Properties

    public override Rect ClipRect
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

    public override float ColorScale
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

    public override Color DrawColor
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

    /// <summary>The current blend mode used for drawing operations.</summary>
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

    /// <summary>The default scale mode used for new textures.</summary>
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

    /// <summary>The current draw color as floats from 0.0 to 1.0.</summary>
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

    public override Rect LogicalRepresentationRect
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetRenderLogicalPresentationRect(_rendererId, out var rect);
            return rect;
        }
    }

    /// <summary>The implementation-defined name of the renderer.</summary>
    public string Name => IsDisposed ? "" : SDL.GetRendererName(_rendererId) ?? "";

    public override (int Width, int Height) OutputSize
    {
        get
        {
            if (IsDisposed)
                return default;
            SDL.GetRenderOutputSize(_rendererId, out var width, out var height);
            return (width, height);
        }
    }

    public override (float ScaleX, float ScaleY) Scale
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

    public override Rect ViewPort
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

    private Texture CreateTexture(int width, int height, PixelFormat pixelFormat, SDL.TextureAccess access)
    {
        ThrowIfDisposed();
        var id = SDL.CreateTexture(_rendererId, (SDL.PixelFormat)pixelFormat, access, width, height);
        if (id == 0)
            throw new InvalidOperationException($"SDL.CreateTexture failed: {SDL.GetError()}");
        return new Texture(this, id);
    }

    private Texture CreateTexture(Image image)
    {
        ThrowIfDisposed();
        image.ThrowIfDisposed();
        var id = SDL.CreateTextureFromSurface(_rendererId, image._imageId);
        return new Texture(this, id);
    }

    private readonly ConditionalWeakTable<Image, ImageTextureEntry> _imageTextureCache = new();

    private sealed class ImageTextureEntry
    {
        public Texture Texture { get; set; } = null!;
        public int Version { get; set; }
    }

    private bool TryGetOrCreateTexture(Image image, [NotNullWhen(true)] out Texture? texture)
    {
        if (!_imageTextureCache.TryGetValue(image, out var entry))
        {
            entry = new ImageTextureEntry
            {
                Texture = CreateTexture(image),
                Version = image.Version,
            };
            _imageTextureCache.AddOrUpdate(image, entry);
        }
        else if (entry.Texture.IsDisposed || entry.Version != image.Version)
        {
            entry.Texture.Dispose();
            entry.Texture = CreateTexture(image);
            entry.Version = image.Version;
        }

        texture = entry.Texture;
        return texture != null;
    }

    #endregion

    #region Rendering

    public override void Clear()
    {
        if (IsDisposed)
            return;
        SDL.RenderClear(_rendererId);
    }

    /// <summary>
    /// Updates the rendering target to display the most recently rendered content.
    /// </summary>
    public void Present()
    {
        ThrowIfDisposed();
        SDL.RenderPresent(_rendererId);
    }

    public override bool RenderDebugText(int x, int y, string text, float scale = 0f)
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

    public override bool RenderImage(Image image, Rect source, Rect destination)
    {
        if (IsDisposed)
            return false;
        if (!TryGetOrCreateTexture(image, out var texture))
            return false;
        return SDL.RenderTexture(_rendererId, texture.Id, source, destination);
    }

    public override bool RenderImageRotated(Image image, Rect source, Rect destination, float angle, Vector2 center, FlipMode flip = FlipMode.None)
    {
        if (IsDisposed)
            return false;
        if (!TryGetOrCreateTexture(image, out var texture))
            return false;
        var sdlCenter = new SDL.FPoint { X = center.X, Y = center.Y };
        return SDL.RenderTextureRotated(_rendererId, texture.Id, source, destination, angle, sdlCenter, (SDL.FlipMode)flip);
    }

    public override bool RenderFillRect(Rect rect)
    {
        SDL.FRect r = rect;
        return SDL.RenderFillRect(_rendererId, r);
    }

    public override bool RenderFillRects(ReadOnlySpan<Rect> rects)
    {
        unsafe
        {
            fixed (Rect* p = rects)
                return SDL3Native.SDL_RenderFillRects(_rendererId, p, rects.Length);
        }
    }

    public override bool RenderGeometry(ReadOnlySpan<Vertex2D> vertices, ReadOnlySpan<int> indices, Image? image = null)
    {
        var texture = image == null ? null
            : TryGetOrCreateTexture(image!, out var txt) ? txt
            : null;

        unsafe
        {
            fixed (Vertex2D* pVertices = vertices)
            fixed (int* pIndices = indices)
            {
                return SDL3Native.SDL_RenderGeometry(
                    _rendererId,
                    texture != null ? texture.Id : 0,
                    pVertices, vertices.Length,
                    pIndices, indices.Length);
            }
        }
    }

    public override bool RenderLine(float x1, float y1, float x2, float y2)
    {
        return SDL.RenderLine(_rendererId, x1, y1, x2, y2);
    }

    public override bool RenderLines(ReadOnlySpan<Vector2> points)
    {
        unsafe
        {
            fixed (Vector2* p = points)
                return SDL3Native.SDL_RenderLines(_rendererId, p, points.Length);
        }
    }

    public override bool RenderPoint(float x, float y)
    {
        return SDL.RenderPoint(_rendererId, x, y);
    }

    public override bool RenderPoints(ReadOnlySpan<Vector2> points)
    {
        unsafe
        {
            fixed (Vector2* p = points)
                return SDL3Native.SDL_RenderPoints(_rendererId, p, points.Length);
        }
    }

    #endregion
}

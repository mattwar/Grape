using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Blitter;

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
        ApplySyncMode(base.SyncMode);
    }

    internal nint RendererId => _rendererId;

    /// <inheritdoc/>
    public override SyncMode SyncMode
    {
        get => base.SyncMode;
        set
        {
            base.SyncMode = value;
            ApplySyncMode(value);
        }
    }

    private void ApplySyncMode(SyncMode mode)
    {
        if (_rendererId == 0)
            return;
        // SDL_Renderer's vsync is just an int interval (0=off, 1=every
        // refresh). Map Latest to vsync as well -- it's the closest
        // tear-free, low-latency option this backend offers.
        var interval = mode == SyncMode.Immediate ? 0 : 1;
        SDL.SetRenderVSync(_rendererId, interval);
    }

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

    public override void SetLogicalSize(int width, int height, LogicalPresentation mode)
    {
        if (IsDisposed)
            return;
        SDL.SetRenderLogicalPresentation(_rendererId, width, height, (SDL.RendererLogicalPresentation)mode);
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

    private BitmapSurface CreateTexture(int width, int height, PixelFormat pixelFormat, SDL.TextureAccess access)
    {
        ThrowIfDisposed();
        var id = SDL.CreateTexture(_rendererId, (SDL.PixelFormat)pixelFormat, access, width, height);
        if (id == 0)
            throw new InvalidOperationException($"SDL.CreateTexture failed: {SDL.GetError()}");
        return new BitmapSurface(this, id);
    }

    private BitmapSurface CreateTexture(Image image)
    {
        ThrowIfDisposed();
        if (image is not Bitmap bitmap)
            throw new NotSupportedException(
                $"BitmapRenderer2D only supports {nameof(Bitmap)} sources; got {image.GetType().Name}.");
        bitmap.ThrowIfDisposed();
        var id = SDL.CreateTextureFromSurface(_rendererId, bitmap._imageId);
        return new BitmapSurface(this, id);
    }

    private readonly ConditionalWeakTable<Image, ImageTextureEntry> _imageTextureCache = new();

    private sealed class ImageTextureEntry
    {
        public BitmapSurface Texture { get; set; } = null!;
        public int Version { get; set; }
    }

    private bool TryGetOrCreateTexture(Image image, [NotNullWhen(true)] out BitmapSurface? texture)
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

    private bool _needsClear = true;

    /// <summary>
    /// If <see cref="Renderer2D.AutoClear"/> is on and the back buffer
    /// hasn't been cleared since the last present, clear it to
    /// <see cref="Renderer2D.BackgroundColor"/>. Called at the top of
    /// every <c>Draw*</c> override so the user never sees an
    /// undefined-content buffer.
    /// </summary>
    private void EnsureCleared()
    {
        if (!AutoClear || !_needsClear)
            return;
        // Save and restore DrawColor so the user's next call sees the
        // value they set (or, if they set nothing, BackgroundColor as a
        // sensible default).
        var saved = this.DrawColor;
        this.DrawColor = BackgroundColor;
        SDL.RenderClear(_rendererId);
        this.DrawColor = saved;
        _needsClear = false;
    }

    public override void Clear()
    {
        if (IsDisposed)
            return;
        SDL.RenderClear(_rendererId);
        _needsClear = false;
    }

    /// <summary>
    /// Presents the queued draws to the screen.
    /// </summary>
    protected override void RenderOnApplicationThread()
    {
        ThrowIfDisposed();
        // Snapshot the frame clock at the START of the render so the next
        // handler's ElapsedSinceLastRender reflects the full frame interval
        // (including this render's own work), not just the gap between
        // present and the next handler invocation.
        AdvanceFrameClock();
        EnsureCleared();
        SDL.RenderPresent(_rendererId);
        _needsClear = true;
    }

    public override bool DrawDebugText(int x, int y, string text, float scale = 0f)
    {
        if (IsDisposed)
            return false;
        EnsureCleared();

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

    public override bool DrawImage(Image image, Rect source, Rect destination)
    {
        if (IsDisposed)
            return false;
        if (!TryGetOrCreateTexture(image, out var texture))
            return false;
        EnsureCleared();
        return SDL.RenderTexture(_rendererId, texture.Id, source, destination);
    }

    public override bool DrawImageRotated(Image image, Rect source, Rect destination, float angle, Vector2 center, FlipMode flip = FlipMode.None)
    {
        if (IsDisposed)
            return false;
        if (!TryGetOrCreateTexture(image, out var texture))
            return false;
        EnsureCleared();
        var sdlCenter = new SDL.FPoint { X = center.X, Y = center.Y };
        return SDL.RenderTextureRotated(_rendererId, texture.Id, source, destination, angle, sdlCenter, (SDL.FlipMode)flip);
    }

    public override bool DrawFillRect(Rect rect)
    {
        EnsureCleared();
        SDL.FRect r = rect;
        return SDL.RenderFillRect(_rendererId, r);
    }

    public override bool DrawFillRects(ReadOnlySpan<Rect> rects)
    {
        EnsureCleared();
        unsafe
        {
            fixed (Rect* p = rects)
                return SDL3Native.SDL_RenderFillRects(_rendererId, p, rects.Length);
        }
    }

    public override bool DrawGeometry(ReadOnlySpan<Vertex2D> vertices, ReadOnlySpan<int> indices, Image? image = null)
    {
        var texture = image == null ? null
            : TryGetOrCreateTexture(image!, out var txt) ? txt
            : null;

        EnsureCleared();
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

    public override bool DrawLine(float x1, float y1, float x2, float y2)
    {
        EnsureCleared();
        return SDL.RenderLine(_rendererId, x1, y1, x2, y2);
    }

    public override bool DrawLines(ReadOnlySpan<Vector2> points)
    {
        EnsureCleared();
        unsafe
        {
            fixed (Vector2* p = points)
                return SDL3Native.SDL_RenderLines(_rendererId, p, points.Length);
        }
    }

    public override bool DrawPoint(float x, float y)
    {
        EnsureCleared();
        return SDL.RenderPoint(_rendererId, x, y);
    }

    public override bool DrawPoints(ReadOnlySpan<Vector2> points)
    {
        EnsureCleared();
        unsafe
        {
            fixed (Vector2* p = points)
                return SDL3Native.SDL_RenderPoints(_rendererId, p, points.Length);
        }
    }

    #endregion
}

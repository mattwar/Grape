using Blitter.Events;

namespace Blitter;

/// <summary>
/// A window that uses the SDL 2D <see cref="Renderer"/> for drawing.
/// </summary>
public class Window2D : Window
{
    private Window2DRenderer _renderer = null!;

    public Window2D(int width, int height, WindowFlags flags = WindowFlags.None)
        : base(width, height, flags)
    {
    }

    public Window2D(WindowFlags flags = WindowFlags.None)
        : base(flags)
    {
    }

    protected override void OnWindowCreated()
    {
        _renderer = Window2DRenderer.Create(this);
        _renderer.BackgroundColor = base.BackgroundColor;
    }

    /// <inheritdoc/>
    public override Color BackgroundColor
    {
        get => _renderer is null ? base.BackgroundColor : _renderer.BackgroundColor;
        set
        {
            base.BackgroundColor = value;
            if (_renderer is not null)
                _renderer.BackgroundColor = value;
        }
    }

    private WindowRenderingEventHandler<Window2D, Renderer2D>? _renderingHandler;

    /// <summary>
    /// Occurs when the window is rendering a frame. The handler receives
    /// the renderer; frame timings are available via
    /// <see cref="Renderer2D.ElapsedSinceStart"/> and
    /// <see cref="Renderer2D.ElapsedSinceLastRender"/>.
    /// </summary>
    public event WindowRenderingEventHandler<Window2D, Renderer2D>? Rendering
    {
        add
        {
            _renderingHandler += value;
            if (!IsClosed) Invalidate();   // ensure a frame fires after subscription
        }
        remove
        {
            _renderingHandler -= value;
        }
    }

    protected override void RaiseRenderingEvent()
    {
        var renderer = _renderer;
        var handler = _renderingHandler;
        if (renderer != null && handler != null)
            handler.Invoke(this, renderer);
    }

    protected override void RenderFrame(Action body)
    {
        var renderer = _renderer;
        if (renderer == null)
            return;

        // The window owns the single per-frame Render() flush. Stray
        // Render() calls from inside the body are suppressed so they
        // don't double-present.
        var prev = renderer.RenderSuppressed;
        renderer.RenderSuppressed = true;
        try
        {
            body();
        }
        finally
        {
            renderer.RenderSuppressed = prev;
        }
        renderer.Render();
    }

    /// <summary>
    /// The 2D renderer that draws into this window. Use it to queue draw
    /// calls and call <see cref="Renderer2D.Render"/> to present.
    /// </summary>
    public Renderer2D Renderer => _renderer
        ?? throw new InvalidOperationException("Renderer is not yet available.");

    /// <summary>
    /// Animates the window by repeatedly calling <paramref name="renderFrame"/> on each frame tick
    /// until <paramref name="shouldContinue"/> returns false, the window is closed, or <paramref name="cancellationToken"/> fires.
    /// </summary>
    public Task RunAsync(Func<bool> shouldContinue, Action<Renderer2D> renderFrame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shouldContinue);
        ArgumentNullException.ThrowIfNull(renderFrame);
        return RunAsync(shouldContinue, () => renderFrame(this.Renderer), cancellationToken);
    }

    /// <summary>
    /// Animates the window by repeatedly calling <paramref name="renderFrame"/> on each frame tick
    /// until the window is closed, or <paramref name="cancellationToken"/> fires.
    /// </summary>
    public Task RunAsync(Action<Renderer2D> renderFrame, CancellationToken cancellationToken = default)
        => RunAsync(static () => true, renderFrame, cancellationToken);

    /// <summary>
    /// A 2D renderer that draws into a <see cref="Window2D"/>'s swapchain.
    /// </summary>
    private sealed class Window2DRenderer : BitmapRenderer2D
    {
        private readonly Window2D _window;

        private Window2DRenderer(Window2D window, nint rendererId)
            : base(rendererId)
        {
            _window = window;
            window.AddResource(this);
        }

        /// <summary>
        /// Creates a renderer for this window.
        /// The window already has a default renderer created when the window is created.
        /// </summary>
        internal static Window2DRenderer Create(Window2D window, string? name = null)
        {
            var rendererId = SDL.CreateRenderer(window.WindowId, name);
            return new Window2DRenderer(window, rendererId);
        }

        /// <summary>
        /// Creates a window gpu renderer with the specified shader format.
        /// </summary>
        internal static Window2DRenderer Create(Window2D window, SDL.GPUShaderFormat format)
        {
            var rendererId = SDL.CreateGPURenderer(window.WindowId, format, out var gpuDeviceId);
            return new Window2DRenderer(window, rendererId);
        }

        /// <summary>The <see cref="Blitter.Window"/> this renderer draws into.</summary>
        public Window Window => _window;

        protected override void OnDisposed()
        {
            _window.RemoveResource(this);
        }
    }
}


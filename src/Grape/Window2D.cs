namespace Grape;

/// <summary>
/// A window that uses the SDL 2D <see cref="Renderer"/> for drawing.
/// </summary>
public class Window2D : Window
{
    private Renderer _renderer = null!;

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
        _renderer = Renderer.Create(this);
    }

    /// <summary>
    /// Occurs when the window is rendering a frame, providing access to the
    /// current rendering context.
    /// </summary>
    private WindowEventHandler<WindowRenderEventArgs<Renderer2D>>? _renderingFrame;

    public event WindowEventHandler<WindowRenderEventArgs<Renderer2D>>? RenderingFrame
    {
        add
        {
            _renderingFrame += value;
            if (!IsDisposed) Invalidate();   // ensure a frame fires after subscription
        }
        remove
        {
            _renderingFrame -= value;
        }
    }

    public virtual void OnRenderingFrame(WindowRenderEventArgs<Renderer2D> args)
    {
        _renderingFrame?.Invoke(this, args);
    }

    protected override void DoRenderFrame(TimeSpan elapsedSinceWindowCreated, TimeSpan elapsedSinceLastFrame)
    {
        // DoRender is called on the render thread
        RenderFrame_AppThread(elapsedSinceWindowCreated, elapsedSinceLastFrame, args => OnRenderingFrame(args));
    }

    /// <summary>
    /// Renders an entire frame (assumes the thread is the app thread)
    /// </summary>
    private void RenderFrame_AppThread(TimeSpan elapsedSinceWindowCreated, TimeSpan elapsedSinceLastFrame, Action<WindowRenderEventArgs<Renderer2D>> renderAction)
    {
        var renderer = _renderer;
        if (renderer != null)
        {
            renderer.DrawColor = this.BackgroundColor;
            renderer.Clear();
            renderAction(new WindowRenderEventArgs<Renderer2D>(elapsedSinceWindowCreated, elapsedSinceLastFrame, renderer));
            renderer.Present();
        }
    }

    /// <summary>
    /// A 2D renderer that draws into a <see cref="Window2D"/>'s swapchain.
    /// </summary>
    private sealed class Renderer : BitmapRenderer2D
    {
        private readonly Window2D _window;

        private Renderer(Window2D window, nint rendererId)
            : base(rendererId)
        {
            _window = window;
            window.AddResource(this);
        }

        /// <summary>
        /// Creates a renderer for this window.
        /// The window already has a default renderer created when the window is created.
        /// </summary>
        internal static Renderer Create(Window2D window, string? name = null)
        {
            var rendererId = SDL.CreateRenderer(window.WindowId, name);
            return new Renderer(window, rendererId);
        }

        /// <summary>
        /// Creates a window gpu renderer with the specified shader format.
        /// </summary>
        internal static Renderer Create(Window2D window, SDL.GPUShaderFormat format)
        {
            var rendererId = SDL.CreateGPURenderer(window.WindowId, format, out var gpuDeviceId);
            return new Renderer(window, rendererId);
        }

        /// <summary>The <see cref="Grape.Window"/> this renderer draws into.</summary>
        public Window Window => _window;

        protected override void OnDisposed()
        {
            _window.RemoveResource(this);
        }
    }
}


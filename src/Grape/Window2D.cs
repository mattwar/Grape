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
    public event WindowEventHandler<Renderer2D>? RenderingFrame;

    public virtual void OnRenderingFrame(Renderer2D renderer)
    {
        this.RenderingFrame?.Invoke(this, renderer);
    }

    protected override void DoRenderFrame()
    {
        // DoRender is called on the render thread
        RenderFrame_AppThread(r => OnRenderingFrame(r));
    }

    /// <summary>
    /// Renders an entire frame (assumes the thread is the app thread)
    /// </summary>
    private void RenderFrame_AppThread(Action<Renderer2D> renderAction)
    {
        var renderer = _renderer;
        if (renderer != null)
        {
            renderer.DrawColor = this.BackgroundColor;
            renderer.Clear();
            renderAction(_renderer);
            renderer.Present();
        }
    }

    /// <summary>
    /// Renders an entire frame using the specified action.
    /// </summary>
    public void RenderFrame(Action<Renderer2D> renderAction)
    {
        // send render action to application main thread
        Application.Current.Send(_ => RenderFrame_AppThread(renderAction));
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

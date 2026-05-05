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

    private WindowEventHandler<WindowRenderEventArgs<Renderer2D>>? _renderingHandler;

    /// <summary>
    /// Occurs when the window is rendering a frame.
    /// </summary>
    public event WindowEventHandler<WindowRenderEventArgs<Renderer2D>>? Rendering
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
        {
            var (sinceCreate, sinceLast) = ConsumeRenderTimings();
            renderer.DrawColor = this.BackgroundColor;
            renderer.Clear();
            handler.Invoke(this, new WindowRenderEventArgs<Renderer2D>(sinceCreate, sinceLast, renderer));
            renderer.Present();
        }
    }

    /// <summary>
    /// Renders now using the provided render action. 
    /// This is an alternative to subscribing to the <see cref="Rendering"/> event.
    /// </summary>
    public void Render(Action<WindowRenderEventArgs<Renderer2D>> renderAction)
    {
        Application.Current.Send(_ =>
        {
            if (this.IsClosed)
                return;
            var renderer = _renderer;
            if (renderer is null)
                return;

            var (sinceCreate, sinceLast) = ConsumeRenderTimings();
            renderer.DrawColor = this.BackgroundColor;
            renderer.Clear();
            renderAction(new WindowRenderEventArgs<Renderer2D>(sinceCreate, sinceLast, renderer));
            renderer.Present();
        });
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


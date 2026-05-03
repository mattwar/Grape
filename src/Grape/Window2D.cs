namespace Grape;

/// <summary>
/// A window that uses the SDL 2D <see cref="Renderer"/> for drawing.
/// </summary>
public class Window2D : Window
{
    private Renderer2D _renderer = null!;

    public Window2D(int width, int height, SDL.WindowFlags flags = SDL.WindowFlags.Resizable)
        : base(width, height, flags)
    {
    }

    public Window2D(SDL.WindowFlags flags = SDL.WindowFlags.Resizable)
        : base(flags)
    {
    }

    /// <summary>
    /// Exposed for extensions to access
    /// </summary>
    internal Renderer2D Renderer => _renderer;

    protected override void OnWindowCreated()
    {
        _renderer = Renderer2D.Create(this);
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
}

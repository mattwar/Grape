namespace SDL3.Model;

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

    protected override void OnWindowCreated()
    {
        _renderer = Renderer2D.Create(this);
    }

    /// <summary>
    /// The current <see cref="Renderer"/> used to draw to the window.
    /// </summary>
    internal Renderer2D Renderer => _renderer;

    /// <summary>
    /// Occurs when the window is rendering a frame, providing access to the
    /// current rendering context.
    /// </summary>
    public event WindowEventHandler<Renderer2D>? Rendering;

    public virtual void OnRendering(Renderer2D renderer)
    {
        this.Rendering?.Invoke(this, renderer);
    }

    protected override void DoRender()
    {
        var renderer = _renderer;
        if (renderer != null)
        {
            renderer.DrawColor = this.BackgroundColor;
            renderer.Clear();
            this.OnRendering(renderer);
            renderer.Present();
        }
    }

    /// <summary>
    /// Render immediately using the specified action.
    /// </summary>
    public void Render(Action<Renderer2D> renderAction)
    {
        var renderer = _renderer;
        if (renderer != null)
        {
            Application.Current.Send(_ =>
            {
                renderer.DrawColor = this.BackgroundColor;
                renderer.Clear();
                renderAction(renderer);
                renderer.Present();
            });
        }
    }
}

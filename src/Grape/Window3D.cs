namespace Grape;

/// <summary>
/// A window claimed for GPU rendering through a <see cref="Renderer3D"/>.
/// </summary>
public class Window3D : Window
{
    private readonly GpuDevice _device;
    private Renderer3D? _renderer;
    private bool _claimed;

    internal Window3D(GpuDevice device, int width, int height, WindowFlags flags = WindowFlags.None)
        : base(width, height, flags)
    {
        _device = device;
    }

    internal Window3D(GpuDevice device, WindowFlags flags = WindowFlags.None)
        : base(flags)
    {
        _device = device;
    }

    public Window3D(int width, int height, WindowFlags flags = WindowFlags.None)
        : this(GpuDevice.Default, width, height, flags)
    {
    }

    public Window3D(WindowFlags flags = WindowFlags.None)
        : this(GpuDevice.Default, flags)
    {
    }

    protected override void OnDispose()
    {
        if (_claimed)
        {
            SDL.ReleaseWindowFromGPUDevice(_device.GpuDeviceID, this.WindowId);
            _claimed = false;
        }
    }

    /// <summary>
    /// Occurs when the window is rendering a frame using the GPU pipeline.
    /// </summary>
    public event WindowEventHandler<Renderer3D>? RenderingFrame;

    public virtual void OnRenderingFrame(Renderer3D renderer)
    {
        this.RenderingFrame?.Invoke(this, renderer);
    }

    protected override void DoRenderFrame()
    {
        RenderFrame_AppThread(r => OnRenderingFrame(r));
    }


    /// <summary>
    /// Renders an entire frame using the specified action (assumes the thread is the app thread).
    /// </summary>
    private void RenderFrame_AppThread(Action<Renderer3D> renderAction)
    {
        // Lazily create the renderer and claim the window for the GPU on
        // first render. We can't do this in OnWindowCreated because the
        // base ctor invokes that hook before the Window3D ctor body runs,
        // so _device hasn't been assigned yet at that point.
        if (_renderer is null)
        {
            _renderer = new Renderer3D(_device);
            _claimed = SDL.ClaimWindowForGPUDevice(_device.GpuDeviceID, this.WindowId);
            if (!_claimed)
                throw new InvalidOperationException(
                    $"Failed to claim window for GPU device " +
                    $"(driver={_device.Driver}, shaderFormat={_device.ShaderFormat}, windowId={this.WindowId}): " +
                    SDL.GetError());
        }

        if (_claimed)
        {
            _renderer.BeginFrame(this);
            renderAction(_renderer);
            _renderer.Present();
        }
    }

    /// <summary>
    /// Renders an entire frame using the specified action (action runs on app thread).
    /// </summary>
    public void RenderFrame(Action<Renderer3D> renderAction)
    {
        // send render action to application main thread
        Application.Current.Send(_ => RenderFrame_AppThread(renderAction));
    }
}

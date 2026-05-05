namespace Grape;

/// <summary>
/// A window claimed for GPU rendering through a <see cref="Renderer3D"/>.
/// </summary>
public class Window3D : Window
{
    private readonly GpuDevice _device;
    private GpuRenderer? _renderer;
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
    private event WindowEventHandler<WindowRenderEventArgs<Renderer3D>>? _renderingFrame;

    public event WindowEventHandler<WindowRenderEventArgs<Renderer3D>>? RenderingFrame
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

    public virtual void OnRenderingFrame(WindowRenderEventArgs<Renderer3D> args)
    {
        _renderingFrame?.Invoke(this, args);
    }

    protected override void DoRenderFrame(TimeSpan elapsedSinceWindowCreated, TimeSpan elapsedSinceLastFrame)
    {
        RenderFrame_AppThread(elapsedSinceWindowCreated, elapsedSinceLastFrame, r => OnRenderingFrame(r));
    }


    /// <summary>
    /// Renders an entire frame using the specified action (assumes the thread is the app thread).
    /// </summary>
    private void RenderFrame_AppThread(TimeSpan elapsedSinceWindowCreated, TimeSpan elapsedSinceLastFrame, Action<WindowRenderEventArgs<Renderer3D>> renderAction)
    {
        // Lazily create the renderer and claim the window for the GPU on
        // first render. We can't do this in OnWindowCreated because the
        // base ctor invokes that hook before the Window3D ctor body runs,
        // so _device hasn't been assigned yet at that point.
        if (_renderer is null)
        {
            _renderer = new GpuRenderer(_device);
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
            renderAction(new WindowRenderEventArgs<Renderer3D>(elapsedSinceWindowCreated, elapsedSinceLastFrame, _renderer));
            _renderer.Present();
        }
    }
}

namespace SDL3.Model;

/// <summary>
/// A window claimed for GPU rendering through a <see cref="Renderer3D"/>.
/// </summary>
public class Window3D : Window
{
    private readonly GpuDevice _device;
    private Renderer3D? _renderer;
    private bool _claimed;

    public Window3D(GpuDevice device, int width, int height, SDL.WindowFlags flags = SDL.WindowFlags.Resizable)
        : base(width, height, flags)
    {
        _device = device;
    }

    public Window3D(GpuDevice device, SDL.WindowFlags flags = SDL.WindowFlags.Resizable)
        : base(flags)
    {
        _device = device;
    }

    public Window3D(int width, int height, SDL.WindowFlags flags = SDL.WindowFlags.Resizable)
        : this(GpuDevice.Default, width, height, flags)
    {
    }

    public Window3D(SDL.WindowFlags flags = SDL.WindowFlags.Resizable)
        : this(GpuDevice.Default, flags)
    {
    }

    /// <summary>
    /// The <see cref="Renderer3D"/> this window draws through. Available
    /// once the first frame has rendered.
    /// </summary>
    public Renderer3D Renderer =>
        _renderer ?? throw new InvalidOperationException(
            "The renderer is not available until the first frame has rendered.");

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
    public event WindowEventHandler<Renderer3D>? Rendering3D;

    public virtual void OnRendering3D(Renderer3D renderer)
    {
        this.Rendering3D?.Invoke(this, renderer);
    }

    protected override void DoRender()
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

        _renderer.BeginFrame(this);
        OnRendering3D(_renderer);
        _renderer.Present();
    }
}

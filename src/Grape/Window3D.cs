using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.Marshalling;

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
        Init();
    }

    internal Window3D(GpuDevice device, WindowFlags flags = WindowFlags.None)
        : base(flags)
    {
        _device = device;
        Init();
    }

    public Window3D(int width, int height, WindowFlags flags = WindowFlags.None)
        : this(GpuDevice.Default, width, height, flags)
    {
    }

    public Window3D(WindowFlags flags = WindowFlags.None)
        : this(GpuDevice.Default, flags)
    {
    }

    private void Init()
    {
        Application.Current.Send(_ =>
        {
            if (!SDL.ClaimWindowForGPUDevice(_device.GpuDeviceID, this.WindowId))
            {
                throw new InvalidOperationException(
                    $"Failed to claim window for GPU device " +
                    $"(driver={_device.Driver}, shaderFormat={_device.ShaderFormat}, windowId={this.WindowId}): " +
                    SDL.GetError());
            }
            _claimed = true;
        });
        _renderer = new GpuRenderer(_device);
    }

    protected override void OnDispose()
    {
        if (_claimed)
        {
            SDL.ReleaseWindowFromGPUDevice(_device.GpuDeviceID, this.WindowId);
            _claimed = false;
        }
    }

    private event WindowEventHandler<WindowRenderEventArgs<Renderer3D>>? _renderingFrame;

    /// <summary>
    /// Raised when the window is rendering a frame.
    /// </summary>
    public event WindowEventHandler<WindowRenderEventArgs<Renderer3D>>? Rendering
    {
        add
        {
            _renderingFrame += value;
            if (!IsClosed) Invalidate();   // ensure a frame fires after subscription
        }
        remove
        {
            _renderingFrame -= value;
        }
    }

    /// <summary>
    /// Called when the window is rendering a frame
    /// </summary>
    public virtual void OnRendering(WindowRenderEventArgs<Renderer3D> args)
    {
        _renderingFrame?.Invoke(this, args);
    }

    protected override void RaiseRenderingEvent()
    {
        if (TryGetRenderer(out var renderer))
        {
            var (elapsedSinceWindowCreated, elapsedSinceLastFrame) = ConsumeRenderTimings();
            renderer.BeginFrame(this);
            OnRendering(new WindowRenderEventArgs<Renderer3D>(elapsedSinceWindowCreated, elapsedSinceLastFrame, renderer));
            renderer.Present();
        }
    }

    /// <summary>
    /// Renders a frame manually using the provided render action.
    /// This is an alternative to subscribing to the <see cref="Rendering"/> event.
    /// </summary>
    public void Render(Action<WindowRenderEventArgs<Renderer3D>> renderAction)
    {
        Application.Current.Send(_ =>
        {
            if (TryGetRenderer(out var renderer))
            {
                var (sinceCreate, sinceLast) = ConsumeRenderTimings();
                renderer.BeginFrame(this);
                renderAction(new WindowRenderEventArgs<Renderer3D>(sinceCreate, sinceLast, renderer));
                renderer.Present();
            }
        });
    }

    private bool TryGetRenderer([NotNullWhen(true)] out GpuRenderer? renderer)
    {
        renderer = _renderer;
        return renderer is not null && _claimed && !this.IsClosed;
    }
}

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
        _renderer = new GpuRenderer(_device, this);
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

    protected override void OnDispose()
    {
        if (_claimed)
        {
            SDL.ReleaseWindowFromGPUDevice(_device.GpuDeviceID, this.WindowId);
            _claimed = false;
        }
    }

    private event WindowEventHandler<Renderer3D>? _renderingFrame;

    /// <summary>
    /// Raised when the window is rendering a frame. The handler receives
    /// the renderer; frame timings are available via
    /// <see cref="Renderer3D.ElapsedSinceStart"/> and
    /// <see cref="Renderer3D.ElapsedSinceLastRender"/>.
    /// </summary>
    public event WindowEventHandler<Renderer3D>? Rendering
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
    /// Called when the window is rendering a frame.
    /// </summary>
    public virtual void OnRendering(Renderer3D renderer)
    {
        _renderingFrame?.Invoke(this, renderer);
    }

    protected override void RaiseRenderingEvent()
    {
        if (TryGetRenderer(out var renderer))
        {
            // The window owns the single per-frame Render() flush in the
            // event-driven path. Stray Render() calls from inside the
            // handler are suppressed so they don't double-present.
            var prev = renderer.RenderSuppressed;
            renderer.RenderSuppressed = true;
            try
            {
                OnRendering(renderer);
            }
            finally
            {
                renderer.RenderSuppressed = prev;
            }
            renderer.Render();
        }
    }

    /// <summary>
    /// The 3D renderer that draws into this window. Use it to queue draw
    /// calls and call <see cref="Renderer3D.Render"/> to present.
    /// </summary>
    public Renderer3D Renderer => _renderer
        ?? throw new InvalidOperationException("Renderer is not yet available.");

    private bool TryGetRenderer([NotNullWhen(true)] out GpuRenderer? renderer)
    {
        renderer = _renderer;
        return renderer is not null && _claimed && !this.IsClosed;
    }
}

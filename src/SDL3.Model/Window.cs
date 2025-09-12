using System.Collections.Immutable;

namespace SDL3.Model;

/// <summary>
/// A window class corresponding to an SDL window.
/// </summary>
public class Window : IDisposable
{
    private nint _window;
    private Properties? _properties;
    private Renderer _renderer;

    public Window(int width, int height, SDL.WindowFlags flags = SDL.WindowFlags.Resizable)
    {
        _window = 0;
        _renderer = null!;

        // start application if it is not already started.
        var app = Application.Start();

        // make sure that window is created on the application thread.
        app.Send(_ =>
        {
            _window = SDL.CreateWindow("", width, height, flags);
            this.Id = SDL.GetWindowID(_window);
            Application.Current.AddWindow(this);
            _renderer = CreateRenderer(null);
        });
    }

    private ImmutableList<IDisposable> _resources = ImmutableList<IDisposable>.Empty;

    internal void AddResource(IDisposable resource)
    {
        ImmutableInterlocked.Update(ref _resources, list => list.Add(resource));
    }

    internal void RemoveResource(IDisposable resource)
    {
        ImmutableInterlocked.Update(ref _resources, list => list.Remove(resource));
    }

    /// <summary>
    /// True if this window has been disposed.
    /// </summary>
    public bool IsDisposed => _window == 0;

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new InvalidOperationException("Window Disposed");
    }

    /// <summary>
    /// Disposes this window, releasing its resources.
    /// </summary>
    public void Dispose()
    {
        if (!IsDisposed)
        {
            var id = Interlocked.Exchange(ref _window, 0);
            if (id != 0)
            {
                OnDispose();

                foreach (var resource in _resources)
                {
                    resource.Dispose();
                }

                SDL.DestroyWindow(id);

                _resources.Clear();
            }
        }
    }

    /// <summary>
    /// Override to perform custom disposal operations.
    /// </summary>
    protected virtual void OnDispose()
    {
    }

    #region properties

    /// <summary>
    /// The id of the window;
    /// </summary>
    internal uint Id { get; private set; }

    /// <summary>
    /// The SDL properties of the window.
    /// </summary>
    private Properties Properties
    {
        get
        {
            if (IsDisposed)
                return Properties.Empty;
            if (_properties == null)
                _properties = new Properties(SDL.GetWindowProperties(_window));
            return _properties;
        }
    }

    private Surface? _icon;

    /// <summary>
    /// The current icon used for the window
    /// </summary>
    public Surface? Icon
    {
        get => _icon;

        set
        {
            if (IsDisposed || value == null)
                return;
            _icon = value;
            SDL.SetWindowIcon(_window, value._surfaceId);
        }
    }

    /// <summary>
    /// The title the window was created with.
    /// </summary>
    public string CreateTitle => this.Properties.GetStringProperty(SDL.Props.WindowCreateTitleString);

    /// <summary>
    /// The width the window was created with.
    /// </summary>
    public int CreateWidth => (int)this.Properties.GetNumberProperty(SDL.Props.WindowCreateWidthNumber);

    /// <summary>
    /// The height the window was created with.
    /// </summary>
    public int CreateHeight => (int)this.Properties.GetNumberProperty(SDL.Props.WindowCreateHeightNumber);

    /// <summary>
    /// The current <see cref="WindowFlags"/> of the window.
    /// </summary>
    public SDL.WindowFlags Flags
    {
        get
        {
            if (IsDisposed)
                return 0;
            return SDL.GetWindowFlags(_window);
        }
    }

    /// <summary>
    /// True if the window is currently focusable.
    /// </summary>
    public bool Focusable
    {
        get 
        {
            if (IsDisposed)
                return false;
            return (Flags & SDL.WindowFlags.NotFocusable) == 0;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowFocusable(_window, value);
        }
    }

    /// <summary>
    /// The aspect ratio of the window.
    /// </summary>
    public (float Min, float Max) AspectRatio
    {
        get
        {
            if (IsDisposed)
                return (0, 0);
            SDL.GetWindowAspectRatio(_window, out float minAspect, out float maxAspect);
            return (minAspect, maxAspect);
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowAspectRatio(_window, value.Min, value.Max);
        }
    }

    /// <summary>
    /// True if the window has a border
    /// </summary>
    public bool Bordered
    {
        get
        {
            if (IsDisposed)
                return false;
            return (Flags & SDL.WindowFlags.Borderless) == 0;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowBordered(_window, value);
        }
    }

    /// <summary>
    /// The size of the window borders in pixels.
    /// </summary>
    public (int Top, int Left, int Bottom, int Right) BorderSize
    {
        get
        {
            if (IsDisposed)
                return (0, 0, 0, 0);
            SDL.GetWindowBordersSize(_window, out int top, out int left, out int bottom, out int right);
            return (top, left, bottom, right);
        }
    }

    /// <summary>
    /// The scale factor for the window's display.
    /// </summary>
    public float DisplayScale
    {
        get
        {
            if (IsDisposed)
                return 1.0f;
            return SDL.GetWindowDisplayScale(_window);
        }
    }

    /// <summary>
    /// True if the window is in full-screen mode.
    /// </summary>
    public bool FullScreen
    {
        get
        {
            if (IsDisposed)
                return false;
            return SDL.GetWindowFullscreenMode(_window) != null;
        }

        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowFullscreen(_window, value);
        }
    }

    /// <summary>
    /// The display mode of the window when in full-screen mode.
    /// </summary>
    public SDL.DisplayMode FullScreenMode
    {
        get
        {
            if (IsDisposed)
                return default;
            return SDL.GetWindowFullscreenMode(_window) ?? default;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowFullscreenMode(_window, value);
        }
    }

    /// <summary>
    /// The minimum size of the window (when it is minimized).
    /// </summary>
    public (int Width, int Height) MinimumSize
    {
        get
        {
            if (IsDisposed)
                return (0, 0);
            SDL.GetWindowMinimumSize(_window, out int width, out int height);
            return (width, height);
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowMinimumSize(_window, value.Width, value.Height);
        }
    }

    /// <summary>
    /// The maximum size of the window (when it is maximized).
    /// </summary>
    public (int Width, int Height) MaximumSize
    {
        get
        {
            if (IsDisposed)
                return (0, 0);
            SDL.GetWindowMaximumSize(_window, out int width, out int height);
            return (width, height);
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowMaximumSize(_window, value.Width, value.Height);
        }
    }

    /// <summary>
    /// The pixel density of the window.
    /// </summary>
    public float PixelDensity
    {
        get
        {
            if (IsDisposed)
                return 1;
            return SDL.GetWindowPixelDensity(_window);
        }
    }

    /// <summary>
    /// The pixel format of the window.
    /// </summary>
    public SDL.PixelFormat PixelFormat
    {
        get
        {
            if (IsDisposed)
                return SDL.PixelFormat.Unknown;
            return SDL.GetWindowPixelFormat(_window);
        }
    }

    /// <summary>
    /// The position of the window on the screen.
    /// </summary>
    public (int X, int Y) Position
    {
        get
        {
            if (IsDisposed)
                return (0, 0);
            SDL.GetWindowPosition(_window, out int x, out int y);
            return (x, y);
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowPosition(_window, value.X, value.Y);
        }
    }

    /// <summary>
    /// The size of the window in pixels.
    /// </summary>
    public (int Width, int Height) Size
    {
        get
        {
            if (IsDisposed)
                return (0, 0);
            SDL.GetWindowSize(_window, out int width, out int height);
            return (width, height);
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowSize(_window, value.Width, value.Height);
        }
    }

    public string Title
    {
        get
        {
            if (IsDisposed)
                return "";
            return SDL.GetWindowTitle(_window);
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowTitle(_window, value);
        }
    }
    #endregion

    #region Rendering

    /// <summary>
    /// Creates a renderer for this window.
    /// The window already has a default renderer created when the window is created.
    /// </summary>
    private Renderer CreateRenderer(string? name)
    {
        ThrowIfDisposed();

        var rendererId = SDL.CreateRenderer(_window, name);
        return new Renderer(this, rendererId, name);
    }

    /// <summary>
    /// The current <see cref="Renderer"/> used to draw to the window.
    /// </summary>
    internal Renderer Renderer => _renderer;

    /// <summary>
    /// The background color used to clear the window before drawing.
    /// </summary>
    public SDL.Color BackgroundColor { get; set; }

    private enum RenderState
    {
        Idle = 0,
        Scheduled,
        Rendering
    }

    private RenderState _renderState;

    /// <summary>
    /// Invalidate's the entire window so it will rerendered.
    /// </summary>
    public void Invalidate()
    {
        if (Interlocked.CompareExchange(ref _renderState, RenderState.Scheduled, RenderState.Idle) == RenderState.Idle)
        {
            Application.Current.Post(_tcs => DoInvalidate(), null);
        }
    }

    private void DoInvalidate()
    {
        Render();
        _renderState = RenderState.Idle;
    }

    private void Render()
    {
        var renderer = _renderer;
        if (renderer != null)
        {
            _renderer.DrawColor = this.BackgroundColor;
            _renderer.Clear();
            this.OnRendering(_renderer);
            _renderer.Present();
        }
    }

    /// <summary>
    /// Occurs when the window is rendering a frame, providing access to the current rendering context.
    /// </summary>
    /// <remarks>This event is raised during the rendering process and allows subscribers to perform custom
    /// rendering  operations or interact with the rendering context. Handlers can use the provided <see
    /// cref="RenderContext"/>  to access rendering-specific data and functionality.</remarks>
    public event WindowEventHandler<Renderer>? Rendering;

    public virtual void OnRendering(Renderer renderer)
    {
        this.Rendering?.Invoke(this, renderer);
    }

    #endregion

    #region events
    internal void DispatchEvent(SDL.Event e)
    {
        var window = this;

        switch ((SDL.EventType)e.Type)
        {
            case SDL.EventType.WindowCloseRequested:
                this.OnWindowCloseRequested(window, e.Window);
                break;
            case SDL.EventType.WindowDestroyed:
                this.OnWindowDestroyed(window, e.Window);
                break;
            case SDL.EventType.WindowDisplayChanged:
                this.OnWindowDisplayChanged(window, e.Window);
                break;
            case SDL.EventType.WindowDisplayScaleChanged:
                this.OnWindowDisplayScaleChanged(window, e.Window);
                break;
            case SDL.EventType.WindowEnterFullscreen:
                this.OnWindowEnterFullscreen(window, e.Window);
                break;
            case SDL.EventType.WindowLeaveFullscreen:
                this.OnWindowLeaveFullscreen(window, e.Window);
                break;
            case SDL.EventType.WindowFocusGained:
                this.OnWindowFocusGained(window, e.Window);
                break;
            case SDL.EventType.WindowFocusLost:
                this.OnWindowFocusLost(window, e.Window);
                break;
            case SDL.EventType.WindowHidden:
                this.OnWindowHidden(window, e.Window);
                break;
            case SDL.EventType.WindowShown:
                this.OnWindowShown(window, e.Window);
                break;
            case SDL.EventType.WindowExposed:
                this.OnWindowExposed(window, e.Window);
                break;
            case SDL.EventType.WindowOccluded:
                this.OnWindowOccluded(window, e.Window);
                break;
            case SDL.EventType.WindowMaximized:
                this.OnWindowMaximized(window, e.Window);
                break;
            case SDL.EventType.WindowMinimized:
                this.OnWindowMinimized(window, e.Window);
                break;
            case SDL.EventType.WindowResized:
                this.OnWindowResized(window, e.Window);
                break;
            case SDL.EventType.WindowRestored:
                this.OnWindowRestored(window, e.Window);
                break;
            case SDL.EventType.WindowMouseEnter:
                this.OnWindowMouseEnter(window, e.Window);
                break;
            case SDL.EventType.WindowMouseLeave:
                this.OnWindowMouseLeave(window, e.Window);
                break;
            case SDL.EventType.WindowMoved:
                this.OnWindowMoved(window, e.Window);
                break;
            case SDL.EventType.WindowPixelSizeChanged:
                this.OnWindowPixelSizeChanged(window, e.Window);
                break;
            case SDL.EventType.WindowSafeAreaChanged:
                this.OnWindowSafeAreaChanged(window, e.Window);
                break;
            case SDL.EventType.WindowHDRStateChanged:
                this.OnWindowHDRStateChanged(window, e.Window);
                break;
            case SDL.EventType.WindowHitTest:
                this.OnWindowHitTest(window, e.Window);
                break;
            case SDL.EventType.WindowICCProfChanged:
                this.OnWindowICCProfChanged(window, e.Window);
                break;
            case SDL.EventType.WindowMetalViewResized:
                this.OnWindowMetalViewResized(window, e.Window);
                break;

            case SDL.EventType.MouseButtonDown:
                this.OnMouseButtonDown(window, e.Button);
                break;
            case SDL.EventType.MouseButtonUp:
                this.OnMouseButtonUp(window, e.Button);
                break;
            case SDL.EventType.MouseMotion:
                this.OnMouseMotion(window, e.Motion);
                break;
            case SDL.EventType.MouseWheel:
                this.OnMouseWheel(window, e.Wheel);
                break;

            case SDL.EventType.KeyDown:
                this.OnKeyDown(window, e.Key);
                break;
            case SDL.EventType.KeyUp:
                this.OnKeyUp(window, e.Key);
                break;
            case SDL.EventType.TextEditing:
                this.OnTextEditing(window, e.Edit);
                break;
            case SDL.EventType.TextInput:
                this.OnTextInput(window, e.Text);
                break;
        }
    }

    #region Keyboard/Text Events
    public event WindowEventHandler<SDL.KeyboardEvent>? KeyDown;
    protected virtual void OnKeyDown(Window window, SDL.KeyboardEvent e) { this.KeyDown?.Invoke(window, e); }

    public event WindowEventHandler<SDL.KeyboardEvent>? KeyUp;
    protected virtual void OnKeyUp(Window window, SDL.KeyboardEvent e) { this.KeyUp?.Invoke(window, e); }

    public event WindowEventHandler<SDL.TextEditingEvent>? TextEditing;
    protected virtual void OnTextEditing(Window window, SDL.TextEditingEvent e) { this.TextEditing?.Invoke(window, e); }

    public event WindowEventHandler<SDL.TextInputEvent>? TextInput;
    protected virtual void OnTextInput(Window window, SDL.TextInputEvent e) { this.TextInput?.Invoke(window, e); }
    #endregion

    #region Window Events
    public event WindowEventHandler<SDL.WindowEvent>? WindowCloseRequested;
    protected virtual void OnWindowCloseRequested(Window window, SDL.WindowEvent e) {
        this.WindowCloseRequested?.Invoke(window, e);
        this.Dispose();
    }

    public event WindowEventHandler<SDL.WindowEvent>? WindowDestroyed;
    protected virtual void OnWindowDestroyed(Window window, SDL.WindowEvent e) { this.WindowDestroyed?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowDisplayChanged;
    protected virtual void OnWindowDisplayChanged(Window window, SDL.WindowEvent e) { this.Invalidate();  this.WindowDisplayChanged?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowDisplayScaleChanged;
    protected virtual void OnWindowDisplayScaleChanged(Window window, SDL.WindowEvent e) { this.Invalidate();  this.WindowDisplayScaleChanged?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowEnterFullscreen;
    protected virtual void OnWindowEnterFullscreen(Window window, SDL.WindowEvent e) { this.Invalidate();  this.WindowEnterFullscreen?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowLeaveFullscreen;
    protected virtual void OnWindowLeaveFullscreen(Window window, SDL.WindowEvent e) { this.Invalidate();  this.WindowLeaveFullscreen?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowFocusGained;
    protected virtual void OnWindowFocusGained(Window window, SDL.WindowEvent e) 
    {
#if !DEBUG
        this.Invalidate();  
#endif
        this.WindowFocusGained?.Invoke(window, e); 
    }

    public event WindowEventHandler<SDL.WindowEvent>? WindowFocusLost;
    protected virtual void OnWindowFocusLost(Window window, SDL.WindowEvent e) 
    {
#if !DEBUG
        this.Invalidate(); 
#endif
        this.WindowFocusLost?.Invoke(window, e); 
    }

    public event WindowEventHandler<SDL.WindowEvent>? WindowHidden;
    protected virtual void OnWindowHidden(Window window, SDL.WindowEvent e) { this.WindowHidden?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowShown;
    protected virtual void OnWindowShown(Window window, SDL.WindowEvent e) { this.Invalidate(); this.WindowShown?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowExposed;
    protected virtual void OnWindowExposed(Window window, SDL.WindowEvent e) { this.Invalidate(); this.WindowExposed?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowOccluded;
    protected virtual void OnWindowOccluded(Window window, SDL.WindowEvent e) { this.WindowOccluded?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowMaximized;
    protected virtual void OnWindowMaximized(Window window, SDL.WindowEvent e) { this.Invalidate(); this.WindowMaximized?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowMinimized;
    protected virtual void OnWindowMinimized(Window window, SDL.WindowEvent e) { this.Invalidate(); this.WindowMinimized?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowResized;
    protected virtual void OnWindowResized(Window window, SDL.WindowEvent e) { this.Invalidate(); this.WindowResized?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowRestored;
    protected virtual void OnWindowRestored(Window window, SDL.WindowEvent e) { this.Invalidate(); this.WindowRestored?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowMouseEnter;
    protected virtual void OnWindowMouseEnter(Window window, SDL.WindowEvent e) { this.WindowMouseEnter?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowMouseLeave;
    protected virtual void OnWindowMouseLeave(Window window, SDL.WindowEvent e) { this.WindowMouseLeave?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowMoved;
    protected virtual void OnWindowMoved(Window window, SDL.WindowEvent e) { this.WindowMoved?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowPixelSizeChanged;
    protected virtual void OnWindowPixelSizeChanged(Window window, SDL.WindowEvent e) { this.Invalidate(); this.WindowPixelSizeChanged?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowSafeAreaChanged;
    protected virtual void OnWindowSafeAreaChanged(Window window, SDL.WindowEvent e) { this.Invalidate(); this.WindowSafeAreaChanged?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowHDRStateChanged;
    protected virtual void OnWindowHDRStateChanged(Window window, SDL.WindowEvent e) { this.Invalidate(); this.WindowHDRStateChanged?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowHitTest;
    protected virtual void OnWindowHitTest(Window window, SDL.WindowEvent e) { this.WindowHitTest?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowICCProfChanged;
    protected virtual void OnWindowICCProfChanged(Window window, SDL.WindowEvent e) { this.WindowICCProfChanged?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowMetalViewResized;
    protected virtual void OnWindowMetalViewResized(Window window, SDL.WindowEvent e) { this.Invalidate(); this.WindowMetalViewResized?.Invoke(window, e); }
#endregion

    #region Mouse Events
    public event WindowEventHandler<SDL.MouseMotionEvent>? MouseMotion;
    protected virtual void OnMouseMotion(Window window, SDL.MouseMotionEvent e) { this.MouseMotion?.Invoke(window, e); }

    public event WindowEventHandler<SDL.MouseButtonEvent>? MouseButtonDown;
    protected virtual void OnMouseButtonDown(Window window, SDL.MouseButtonEvent e) { this.MouseButtonDown?.Invoke(window, e); }

    public event WindowEventHandler<SDL.MouseButtonEvent>? MouseButtonUp;
    protected virtual void OnMouseButtonUp(Window window, SDL.MouseButtonEvent e) { this.MouseButtonUp?.Invoke(window, e); }

    public event WindowEventHandler<SDL.MouseWheelEvent>? MouseWheel;
    protected virtual void OnMouseWheel(Window window, SDL.MouseWheelEvent e) { this.MouseWheel?.Invoke(window, e); }
    #endregion

#endregion
}
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Grape;

/// <summary>
/// A window class corresponding to an SDL window.
/// </summary>
public abstract class Window : IDisposable
{
    private nint _window;
    private Properties? _properties;
    private Image? _icon;

    protected Window(int width, int height, WindowFlags flags = WindowFlags.None)
    {
        _window = 0;

        // start application if it is not already started.
        var app = Application.Start();

        // make sure that window is created on the application thread.
        app.Send(_ =>
        {
            // Bring up the video subsystem the first time a window is created.
            // SDL refcounts subsystem init so calling this repeatedly is fine.
            if (!SDL.InitSubSystem(SDL.InitFlags.Video))
                throw new InvalidOperationException(
                    $"Failed to initialize SDL video subsystem: {SDL.GetError()}");

            // Windows are resizable by default. Anything else can be toggled
            // via properties after creation.
            var sdlFlags = (SDL.WindowFlags)flags | SDL.WindowFlags.Resizable;
            _window = SDL.CreateWindow("", width, height, sdlFlags);
            this.EventId = SDL.GetWindowID(_window);
            Application.Current.AddWindow(this);
            OnWindowCreated();
        });
    }

    protected Window(WindowFlags flags = WindowFlags.None)
        : this(100, 100, flags)
    {
    }

    /// <summary>
    /// Called once on the application thread immediately after the underlying
    /// SDL window has been created. Subclasses can override to attach their
    /// own renderer or claim the window for a graphics device.
    /// </summary>
    protected virtual void OnWindowCreated()
    {
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
    /// The underlying SDL window id.
    /// </summary>
    internal nint WindowId => _window;

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
    /// The id of the window in events.
    /// </summary>
    internal uint EventId { get; private set; }

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

    /// <summary>
    /// The display that the window is currently on.
    /// </summary>
    public Display Display
    {
        get
        {
            ThrowIfDisposed();
            var displayId = SDL.GetDisplayForWindow(_window);
            if (Display.TryGetDisplay(displayId, out var display))
                return display;
            throw new InvalidOperationException("Display not found");
        }
    }

    /// <summary>
    /// The current icon used for the window
    /// </summary>
    public Image? Icon
    {
        get => _icon;

        set
        {
            if (IsDisposed || value == null)
                return;
            _icon = value;
            SDL.SetWindowIcon(_window, value._imageId);
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
    /// The creation-time <see cref="WindowFlags"/> of this window. Reflects
    /// only flags that cannot change after creation; runtime state is
    /// available through individual properties.
    /// </summary>
    public WindowFlags Flags
    {
        get
        {
            if (IsDisposed)
                return WindowFlags.None;
            const ulong creationMask =
                (ulong)WindowFlags.OpenGL
                | (ulong)WindowFlags.External
                | (ulong)WindowFlags.HighPixelDensity
                | (ulong)WindowFlags.Utility
                | (ulong)WindowFlags.Tooltip
                | (ulong)WindowFlags.PopupMenu
                | (ulong)WindowFlags.FillDocument
                | (ulong)WindowFlags.Vulkan
                | (ulong)WindowFlags.Metal
                | (ulong)WindowFlags.Transparent;
            return (WindowFlags)((ulong)SDL.GetWindowFlags(_window) & creationMask);
        }
    }

    /// <summary>
    /// True if the window can currently receive keyboard focus.
    /// </summary>
    public bool Focusable
    {
        get
        {
            if (IsDisposed)
                return false;
            return (SDL.GetWindowFlags(_window) & SDL.WindowFlags.NotFocusable) == 0;
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
    /// True if the window has a border.
    /// </summary>
    public bool Bordered
    {
        get
        {
            if (IsDisposed)
                return false;
            return (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Borderless) == 0;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowBordered(_window, value);
        }
    }

    /// <summary>True if the window can be resized by the user.</summary>
    public bool Resizable
    {
        get
        {
            if (IsDisposed)
                return false;
            return (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Resizable) != 0;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowResizable(_window, value);
        }
    }

    /// <summary>True if the window stays above all other windows.</summary>
    public bool AlwaysOnTop
    {
        get
        {
            if (IsDisposed)
                return false;
            return (SDL.GetWindowFlags(_window) & SDL.WindowFlags.AlwaysOnTop) != 0;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowAlwaysOnTop(_window, value);
        }
    }

    /// <summary>True if the window has captured the mouse cursor.</summary>
    public bool MouseGrabbed
    {
        get
        {
            if (IsDisposed)
                return false;
            return SDL.GetWindowMouseGrab(_window);
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowMouseGrab(_window, value);
        }
    }

    /// <summary>True if the window has grabbed keyboard input.</summary>
    public bool KeyboardGrabbed
    {
        get
        {
            if (IsDisposed)
                return false;
            return SDL.GetWindowKeyboardGrab(_window);
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowKeyboardGrab(_window, value);
        }
    }

    /// <summary>True if the window is in relative mouse mode.</summary>
    public bool RelativeMouseMode
    {
        get
        {
            if (IsDisposed)
                return false;
            return SDL.GetWindowRelativeMouseMode(_window);
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowRelativeMouseMode(_window, value);
        }
    }

    /// <summary>True if the window is currently modal to its parent.</summary>
    public bool Modal
    {
        get
        {
            if (IsDisposed)
                return false;
            return (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Modal) != 0;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowModal(_window, value);
        }
    }

    /// <summary>True if the window is currently hidden.</summary>
    public bool IsHidden =>
        !IsDisposed && (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Hidden) != 0;

    /// <summary>True if the window is currently minimized.</summary>
    public bool IsMinimized =>
        !IsDisposed && (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Minimized) != 0;

    /// <summary>True if the window is currently maximized.</summary>
    public bool IsMaximized =>
        !IsDisposed && (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Maximized) != 0;

    /// <summary>True if the window is currently occluded.</summary>
    public bool IsOccluded =>
        !IsDisposed && (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Occluded) != 0;

    /// <summary>True if the window currently has keyboard input focus.</summary>
    public bool HasInputFocus =>
        !IsDisposed && (SDL.GetWindowFlags(_window) & SDL.WindowFlags.InputFocus) != 0;

    /// <summary>True if the window currently has mouse focus.</summary>
    public bool HasMouseFocus =>
        !IsDisposed && (SDL.GetWindowFlags(_window) & SDL.WindowFlags.MouseFocus) != 0;

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
    public DisplayMode FullScreenMode
    {
        get
        {
            if (IsDisposed)
                return default;
            return SDL.GetWindowFullscreenMode(_window) is { } mode
                ? new DisplayMode(mode)
                : default;
        }
        set
        {
            if (IsDisposed)
                return;
            SDL.SetWindowFullscreenMode(_window, value._mode);
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
    /// The position of the window in the multi-display space.
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

    /// <summary>
    /// The title of the window.
    /// </summary>
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

    #region Window state methods

    /// <summary>Shows the window if it was hidden.</summary>
    public void Show()
    {
        if (IsDisposed) return;
        SDL.ShowWindow(_window);
    }

    /// <summary>Hides the window without destroying it.</summary>
    public void Hide()
    {
        if (IsDisposed) return;
        SDL.HideWindow(_window);
    }

    /// <summary>Minimizes the window to the taskbar/dock.</summary>
    public void Minimize()
    {
        if (IsDisposed) return;
        SDL.MinimizeWindow(_window);
    }

    /// <summary>Maximizes the window to fill its display.</summary>
    public void Maximize()
    {
        if (IsDisposed) return;
        SDL.MaximizeWindow(_window);
    }

    /// <summary>
    /// Restores the window from a minimized or maximized state to its
    /// previous size and position.
    /// </summary>
    public void Restore()
    {
        if (IsDisposed) return;
        SDL.RestoreWindow(_window);
    }

    /// <summary>Brings the window above other windows and gives it focus.</summary>
    public void Raise()
    {
        if (IsDisposed) return;
        SDL.RaiseWindow(_window);
    }

    #endregion

    #region Rendering
    /// <summary>
    /// The background color used to clear the window before rendering.
    /// </summary>
    public Color BackgroundColor { get; set; }

    private enum RenderState
    {
        Idle = 0,
        Scheduled,
        Rendering
    }

    private RenderState _renderState;

    /// <summary>
    /// Invalidates the window, scheduling a new render operation.
    /// </summary>
    public void Invalidate()
    {
        if (Interlocked.CompareExchange(ref _renderState, RenderState.Scheduled, RenderState.Idle) == RenderState.Idle)
        {
            Application.Current.Post(_tcs => DoRenderInternal(), null);
        }
    }

    private void DoRenderInternal()
    {
        try
        {
            DoRenderFrame();
        }
        finally
        {
            // signal that we are done rendering
            _renderState = RenderState.Idle;
        }
    }

    /// <summary>
    /// Performs the per-frame rendering for this window. Implementations are
    /// invoked on the application thread.
    /// </summary>
    protected abstract void DoRenderFrame();

    /// <summary>
    /// Render immediately.
    /// </summary>
    public void Render()
    {
        Application.Current.Send(_ => DoRenderInternal(), null);
    }

    #endregion

    #region events
    internal void DispatchEvent(SDL.Event e)
    {
        var window = this;

        switch ((SDL.EventType)e.Type)
        {
            case SDL.EventType.WindowCloseRequested:
                this.OnWindowCloseRequested(window, default);
                break;
            case SDL.EventType.WindowDestroyed:
                this.OnWindowDestroyed(window, default);
                break;
            case SDL.EventType.WindowDisplayChanged:
                Display.TryGetDisplay((uint)e.Window.Data1, out var newDisplay);
                this.OnWindowDisplayChanged(window, new WindowDisplayChangedEventArgs(newDisplay));
                break;
            case SDL.EventType.WindowDisplayScaleChanged:
                this.OnWindowDisplayScaleChanged(window, default);
                break;
            case SDL.EventType.WindowEnterFullscreen:
                this.OnWindowEnterFullscreen(window, default);
                break;
            case SDL.EventType.WindowLeaveFullscreen:
                this.OnWindowLeaveFullscreen(window, default);
                break;
            case SDL.EventType.WindowFocusGained:
                this.OnWindowFocusGained(window, default);
                break;
            case SDL.EventType.WindowFocusLost:
                this.OnWindowFocusLost(window, default);
                break;
            case SDL.EventType.WindowHidden:
                this.OnWindowHidden(window, default);
                break;
            case SDL.EventType.WindowShown:
                this.OnWindowShown(window, default);
                break;
            case SDL.EventType.WindowExposed:
                this.OnWindowExposed(window, default);
                break;
            case SDL.EventType.WindowOccluded:
                this.OnWindowOccluded(window, default);
                break;
            case SDL.EventType.WindowMaximized:
                this.OnWindowMaximized(window, default);
                break;
            case SDL.EventType.WindowMinimized:
                this.OnWindowMinimized(window, default);
                break;
            case SDL.EventType.WindowResized:
                this.OnWindowResized(window, new WindowResizedEventArgs(e.Window.Data1, e.Window.Data2));
                break;
            case SDL.EventType.WindowRestored:
                this.OnWindowRestored(window, default);
                break;
            case SDL.EventType.WindowMouseEnter:
                this.OnWindowMouseEnter(window, default);
                break;
            case SDL.EventType.WindowMouseLeave:
                this.OnWindowMouseLeave(window, default);
                break;
            case SDL.EventType.WindowMoved:
                this.OnWindowMoved(window, new WindowMovedEventArgs(e.Window.Data1, e.Window.Data2));
                break;
            case SDL.EventType.WindowPixelSizeChanged:
                this.OnWindowPixelSizeChanged(window, new WindowPixelSizeChangedEventArgs(e.Window.Data1, e.Window.Data2));
                break;
            case SDL.EventType.WindowSafeAreaChanged:
                this.OnWindowSafeAreaChanged(window, default);
                break;
            case SDL.EventType.WindowHDRStateChanged:
                this.OnWindowHDRStateChanged(window, default);
                break;
            case SDL.EventType.WindowHitTest:
                this.OnWindowHitTest(window, default);
                break;
            case SDL.EventType.WindowICCProfChanged:
                this.OnWindowICCProfChanged(window, default);
                break;
            case SDL.EventType.WindowMetalViewResized:
                this.OnWindowMetalViewResized(window, new WindowMetalViewResizedEventArgs(e.Window.Data1, e.Window.Data2));
                break;

            case SDL.EventType.MouseButtonDown:
                this.OnMouseButtonDown(window, EventArgsFactory.MouseButton(e.Button));
                break;
            case SDL.EventType.MouseButtonUp:
                this.OnMouseButtonUp(window, EventArgsFactory.MouseButton(e.Button));
                break;
            case SDL.EventType.MouseMotion:
                this.OnMouseMotion(window, EventArgsFactory.MouseMove(e.Motion));
                break;
            case SDL.EventType.MouseWheel:
                this.OnMouseWheel(window, EventArgsFactory.MouseWheel(e.Wheel));
                break;

            case SDL.EventType.KeyDown:
                this.OnKeyDown(window, EventArgsFactory.Key(e.Key));
                break;
            case SDL.EventType.KeyUp:
                this.OnKeyUp(window, EventArgsFactory.Key(e.Key));
                break;
            case SDL.EventType.TextEditing:
                this.OnTextEditing(window, EventArgsFactory.TextEditing(e.Edit));
                break;
            case SDL.EventType.TextInput:
                this.OnTextInput(window, EventArgsFactory.TextInput(e.Text));
                break;
        }
    }

    #region Keyboard/Text Events
    public event WindowEventHandler<KeyEventArgs>? KeyDown;
    protected virtual void OnKeyDown(Window window, KeyEventArgs e) { this.KeyDown?.Invoke(window, e); }

    public event WindowEventHandler<KeyEventArgs>? KeyUp;
    protected virtual void OnKeyUp(Window window, KeyEventArgs e) { this.KeyUp?.Invoke(window, e); }

    public event WindowEventHandler<TextEditingEventArgs>? TextEditing;
    protected virtual void OnTextEditing(Window window, TextEditingEventArgs e) { this.TextEditing?.Invoke(window, e); }

    public event WindowEventHandler<TextInputEventArgs>? TextInput;
    protected virtual void OnTextInput(Window window, TextInputEventArgs e) { this.TextInput?.Invoke(window, e); }
    #endregion

    #region Window Events
    public event WindowEventHandler<WindowCloseRequestedEventArgs>? WindowCloseRequested;
    protected virtual void OnWindowCloseRequested(Window window, WindowCloseRequestedEventArgs e) {
        this.WindowCloseRequested?.Invoke(window, e);
        this.Dispose();
    }

    public event WindowEventHandler<WindowDestroyedEventArgs>? WindowDestroyed;
    protected virtual void OnWindowDestroyed(Window window, WindowDestroyedEventArgs e) { this.WindowDestroyed?.Invoke(window, e); }

    public event WindowEventHandler<WindowDisplayChangedEventArgs>? WindowDisplayChanged;
    protected virtual void OnWindowDisplayChanged(Window window, WindowDisplayChangedEventArgs e) { this.Invalidate();  this.WindowDisplayChanged?.Invoke(window, e); }

    public event WindowEventHandler<WindowDisplayScaleChangedEventArgs>? WindowDisplayScaleChanged;
    protected virtual void OnWindowDisplayScaleChanged(Window window, WindowDisplayScaleChangedEventArgs e) { this.Invalidate();  this.WindowDisplayScaleChanged?.Invoke(window, e); }

    public event WindowEventHandler<WindowEnterFullscreenEventArgs>? WindowEnterFullscreen;
    protected virtual void OnWindowEnterFullscreen(Window window, WindowEnterFullscreenEventArgs e) { this.Invalidate();  this.WindowEnterFullscreen?.Invoke(window, e); }

    public event WindowEventHandler<WindowLeaveFullscreenEventArgs>? WindowLeaveFullscreen;
    protected virtual void OnWindowLeaveFullscreen(Window window, WindowLeaveFullscreenEventArgs e) { this.Invalidate();  this.WindowLeaveFullscreen?.Invoke(window, e); }

    public event WindowEventHandler<WindowFocusGainedEventArgs>? WindowFocusGained;
    protected virtual void OnWindowFocusGained(Window window, WindowFocusGainedEventArgs e)
    {
#if !DEBUG
        this.Invalidate();  
#endif
        this.WindowFocusGained?.Invoke(window, e); 
    }

    public event WindowEventHandler<WindowFocusLostEventArgs>? WindowFocusLost;
    protected virtual void OnWindowFocusLost(Window window, WindowFocusLostEventArgs e)
    {
#if !DEBUG
        this.Invalidate(); 
#endif
        this.WindowFocusLost?.Invoke(window, e); 
    }

    public event WindowEventHandler<WindowHiddenEventArgs>? WindowHidden;
    protected virtual void OnWindowHidden(Window window, WindowHiddenEventArgs e) { this.WindowHidden?.Invoke(window, e); }

    public event WindowEventHandler<WindowShownEventArgs>? WindowShown;
    protected virtual void OnWindowShown(Window window, WindowShownEventArgs e) { this.Invalidate(); this.WindowShown?.Invoke(window, e); }

    public event WindowEventHandler<WindowExposedEventArgs>? WindowExposed;
    protected virtual void OnWindowExposed(Window window, WindowExposedEventArgs e) { this.Invalidate(); this.WindowExposed?.Invoke(window, e); }

    public event WindowEventHandler<WindowOccludedEventArgs>? WindowOccluded;
    protected virtual void OnWindowOccluded(Window window, WindowOccludedEventArgs e) { this.WindowOccluded?.Invoke(window, e); }

    public event WindowEventHandler<WindowMaximizedEventArgs>? WindowMaximized;
    protected virtual void OnWindowMaximized(Window window, WindowMaximizedEventArgs e) { this.Invalidate(); this.WindowMaximized?.Invoke(window, e); }

    public event WindowEventHandler<WindowMinimizedEventArgs>? WindowMinimized;
    protected virtual void OnWindowMinimized(Window window, WindowMinimizedEventArgs e) { this.Invalidate(); this.WindowMinimized?.Invoke(window, e); }

    public event WindowEventHandler<WindowResizedEventArgs>? WindowResized;
    protected virtual void OnWindowResized(Window window, WindowResizedEventArgs e) { this.Invalidate(); this.WindowResized?.Invoke(window, e); }

    public event WindowEventHandler<WindowRestoredEventArgs>? WindowRestored;
    protected virtual void OnWindowRestored(Window window, WindowRestoredEventArgs e) { this.Invalidate(); this.WindowRestored?.Invoke(window, e); }

    public event WindowEventHandler<WindowMouseEnterEventArgs>? WindowMouseEnter;
    protected virtual void OnWindowMouseEnter(Window window, WindowMouseEnterEventArgs e) { this.WindowMouseEnter?.Invoke(window, e); }

    public event WindowEventHandler<WindowMouseLeaveEventArgs>? WindowMouseLeave;
    protected virtual void OnWindowMouseLeave(Window window, WindowMouseLeaveEventArgs e) { this.WindowMouseLeave?.Invoke(window, e); }

    public event WindowEventHandler<WindowMovedEventArgs>? WindowMoved;
    protected virtual void OnWindowMoved(Window window, WindowMovedEventArgs e) { this.WindowMoved?.Invoke(window, e); }

    public event WindowEventHandler<WindowPixelSizeChangedEventArgs>? WindowPixelSizeChanged;
    protected virtual void OnWindowPixelSizeChanged(Window window, WindowPixelSizeChangedEventArgs e) { this.Invalidate(); this.WindowPixelSizeChanged?.Invoke(window, e); }

    public event WindowEventHandler<WindowSafeAreaChangedEventArgs>? WindowSafeAreaChanged;
    protected virtual void OnWindowSafeAreaChanged(Window window, WindowSafeAreaChangedEventArgs e) { this.Invalidate(); this.WindowSafeAreaChanged?.Invoke(window, e); }

    public event WindowEventHandler<WindowHDRStateChangedEventArgs>? WindowHDRStateChanged;
    protected virtual void OnWindowHDRStateChanged(Window window, WindowHDRStateChangedEventArgs e) { this.Invalidate(); this.WindowHDRStateChanged?.Invoke(window, e); }

    public event WindowEventHandler<WindowHitTestEventArgs>? WindowHitTest;
    protected virtual void OnWindowHitTest(Window window, WindowHitTestEventArgs e) { this.WindowHitTest?.Invoke(window, e); }

    public event WindowEventHandler<WindowICCProfChangedEventArgs>? WindowICCProfChanged;
    protected virtual void OnWindowICCProfChanged(Window window, WindowICCProfChangedEventArgs e) { this.WindowICCProfChanged?.Invoke(window, e); }

    public event WindowEventHandler<WindowMetalViewResizedEventArgs>? WindowMetalViewResized;
    protected virtual void OnWindowMetalViewResized(Window window, WindowMetalViewResizedEventArgs e) { this.Invalidate(); this.WindowMetalViewResized?.Invoke(window, e); }
#endregion

    #region Mouse Events
    public event WindowEventHandler<MouseMoveEventArgs>? MouseMotion;
    protected virtual void OnMouseMotion(Window window, MouseMoveEventArgs e) { this.MouseMotion?.Invoke(window, e); }

    public event WindowEventHandler<MouseButtonEventArgs>? MouseButtonDown;
    protected virtual void OnMouseButtonDown(Window window, MouseButtonEventArgs e) { this.MouseButtonDown?.Invoke(window, e); }

    public event WindowEventHandler<MouseButtonEventArgs>? MouseButtonUp;
    protected virtual void OnMouseButtonUp(Window window, MouseButtonEventArgs e) { this.MouseButtonUp?.Invoke(window, e); }

    public event WindowEventHandler<MouseWheelEventArgs>? MouseWheel;
    protected virtual void OnMouseWheel(Window window, MouseWheelEventArgs e) { this.MouseWheel?.Invoke(window, e); }
    #endregion

#endregion
}

#region Grape event arg types

/// <summary>Mouse buttons.</summary>
public enum MouseButton : byte
{
    Left = 1,
    Middle = 2,
    Right = 3,
    X1 = 4,
    X2 = 5,
}

/// <summary>Mouse button state flags.</summary>
[Flags]
public enum MouseButtons : uint
{
    None = 0,
    Left = 1u << 0,
    Middle = 1u << 1,
    Right = 1u << 2,
    X1 = 1u << 3,
    X2 = 1u << 4,
}

/// <summary>Mouse wheel scroll direction.</summary>
public enum MouseWheelDirection
{
    Normal = 0,
    Flipped = 1,
}

/// <summary>Keyboard modifier flags.</summary>
[Flags]
public enum KeyModifiers : ushort
{
    None     = 0x0000,
    LShift   = 0x0001,
    RShift   = 0x0002,
    Level5   = 0x0004,
    LCtrl    = 0x0040,
    RCtrl    = 0x0080,
    LAlt     = 0x0100,
    RAlt     = 0x0200,
    LGui     = 0x0400,
    RGui     = 0x0800,
    Num      = 0x1000,
    Caps     = 0x2000,
    Mode     = 0x4000,
    Scroll   = 0x8000,
    Ctrl     = LCtrl | RCtrl,
    Shift    = LShift | RShift,
    Alt      = LAlt | RAlt,
    Gui      = LGui | RGui,
}

/// <summary>
/// Virtual key codes. Values mirror the underlying SDL3 keycode values exactly,
/// so unknown values can be safely cast from the underlying uint.
/// </summary>
public enum Key : uint
{
    Unknown = 0x00000000u,
    Backspace = 0x00000008u,
    Tab = 0x00000009u,
    Return = 0x0000000du,
    Escape = 0x0000001bu,
    Space = 0x00000020u,
    Exclaim = 0x00000021u,
    Hash = 0x00000023u,
    Dollar = 0x00000024u,
    Percent = 0x00000025u,
    Ampersand = 0x00000026u,
    Apostrophe = 0x00000027u,
    LeftParen = 0x00000028u,
    RightParen = 0x00000029u,
    Asterisk = 0x0000002au,
    Plus = 0x0000002bu,
    Comma = 0x0000002cu,
    Minus = 0x0000002du,
    Period = 0x0000002eu,
    Slash = 0x0000002fu,
    D0 = 0x00000030u,
    D1 = 0x00000031u,
    D2 = 0x00000032u,
    D3 = 0x00000033u,
    D4 = 0x00000034u,
    D5 = 0x00000035u,
    D6 = 0x00000036u,
    D7 = 0x00000037u,
    D8 = 0x00000038u,
    D9 = 0x00000039u,
    Colon = 0x0000003au,
    Semicolon = 0x0000003bu,
    Less = 0x0000003cu,
    Equals = 0x0000003du,
    Greater = 0x0000003eu,
    Question = 0x0000003fu,
    At = 0x00000040u,
    LeftBracket = 0x0000005bu,
    Backslash = 0x0000005cu,
    RightBracket = 0x0000005du,
    Caret = 0x0000005eu,
    Underscore = 0x0000005fu,
    Grave = 0x00000060u,
    A = 0x00000061u,
    B = 0x00000062u,
    C = 0x00000063u,
    D = 0x00000064u,
    E = 0x00000065u,
    F = 0x00000066u,
    G = 0x00000067u,
    H = 0x00000068u,
    I = 0x00000069u,
    J = 0x0000006au,
    K = 0x0000006bu,
    L = 0x0000006cu,
    M = 0x0000006du,
    N = 0x0000006eu,
    O = 0x0000006fu,
    P = 0x00000070u,
    Q = 0x00000071u,
    R = 0x00000072u,
    S = 0x00000073u,
    T = 0x00000074u,
    U = 0x00000075u,
    V = 0x00000076u,
    W = 0x00000077u,
    X = 0x00000078u,
    Y = 0x00000079u,
    Z = 0x0000007au,
    Delete = 0x0000007fu,
    CapsLock = 0x40000039u,
    F1 = 0x4000003au,
    F2 = 0x4000003bu,
    F3 = 0x4000003cu,
    F4 = 0x4000003du,
    F5 = 0x4000003eu,
    F6 = 0x4000003fu,
    F7 = 0x40000040u,
    F8 = 0x40000041u,
    F9 = 0x40000042u,
    F10 = 0x40000043u,
    F11 = 0x40000044u,
    F12 = 0x40000045u,
    PrintScreen = 0x40000046u,
    ScrollLock = 0x40000047u,
    Pause = 0x40000048u,
    Insert = 0x40000049u,
    Home = 0x4000004au,
    PageUp = 0x4000004bu,
    End = 0x4000004du,
    PageDown = 0x4000004eu,
    Right = 0x4000004fu,
    Left = 0x40000050u,
    Down = 0x40000051u,
    Up = 0x40000052u,
    NumLockClear = 0x40000053u,
    KpDivide = 0x40000054u,
    KpMultiply = 0x40000055u,
    KpMinus = 0x40000056u,
    KpPlus = 0x40000057u,
    KpEnter = 0x40000058u,
    Kp0 = 0x40000062u,
    Kp1 = 0x40000059u,
    Kp2 = 0x4000005au,
    Kp3 = 0x4000005bu,
    Kp4 = 0x4000005cu,
    Kp5 = 0x4000005du,
    Kp6 = 0x4000005eu,
    Kp7 = 0x4000005fu,
    Kp8 = 0x40000060u,
    Kp9 = 0x40000061u,
    KpPeriod = 0x40000063u,
    Application = 0x40000065u,
    LCtrl = 0x400000e0u,
    LShift = 0x400000e1u,
    LAlt = 0x400000e2u,
    LGui = 0x400000e3u,
    RCtrl = 0x400000e4u,
    RShift = 0x400000e5u,
    RAlt = 0x400000e6u,
    RGui = 0x400000e7u,
}

public readonly record struct KeyEventArgs(
    Key Key,
    KeyModifiers Modifiers,
    bool IsDown,
    bool IsRepeat);

public readonly record struct MouseMoveEventArgs(
    Vector2 Position,
    Vector2 Delta,
    MouseButtons Buttons);

public readonly record struct MouseButtonEventArgs(
    MouseButton Button,
    bool IsDown,
    int Clicks,
    Vector2 Position);

public readonly record struct MouseWheelEventArgs(
    Vector2 Scroll,
    Vector2 MousePosition,
    MouseWheelDirection Direction);

public readonly record struct TextInputEventArgs(string Text);

public readonly record struct TextEditingEventArgs(string Text, int Start, int Length);

// Window event args -- one per window event variant. Most are payload-less
// markers so signatures are stable if data is added later.
public readonly record struct WindowCloseRequestedEventArgs;
public readonly record struct WindowDestroyedEventArgs;
public readonly record struct WindowDisplayChangedEventArgs(Display? Display);
public readonly record struct WindowDisplayScaleChangedEventArgs;
public readonly record struct WindowEnterFullscreenEventArgs;
public readonly record struct WindowLeaveFullscreenEventArgs;
public readonly record struct WindowFocusGainedEventArgs;
public readonly record struct WindowFocusLostEventArgs;
public readonly record struct WindowHiddenEventArgs;
public readonly record struct WindowShownEventArgs;
public readonly record struct WindowExposedEventArgs;
public readonly record struct WindowOccludedEventArgs;
public readonly record struct WindowMaximizedEventArgs;
public readonly record struct WindowMinimizedEventArgs;
public readonly record struct WindowResizedEventArgs(int Width, int Height);
public readonly record struct WindowRestoredEventArgs;
public readonly record struct WindowMouseEnterEventArgs;
public readonly record struct WindowMouseLeaveEventArgs;
public readonly record struct WindowMovedEventArgs(int X, int Y);
public readonly record struct WindowPixelSizeChangedEventArgs(int Width, int Height);
public readonly record struct WindowSafeAreaChangedEventArgs;
public readonly record struct WindowHDRStateChangedEventArgs;
public readonly record struct WindowHitTestEventArgs;
public readonly record struct WindowICCProfChangedEventArgs;
public readonly record struct WindowMetalViewResizedEventArgs(int Width, int Height);

internal static class EventArgsFactory
{
    public static KeyEventArgs Key(SDL.KeyboardEvent e)
        => new((Key)e.Key, (KeyModifiers)e.Mod, e.Down, e.Repeat);

    public static MouseMoveEventArgs MouseMove(SDL.MouseMotionEvent e)
        => new(new Vector2(e.X, e.Y), new Vector2(e.XRel, e.YRel), (MouseButtons)e.State);

    public static MouseButtonEventArgs MouseButton(SDL.MouseButtonEvent e)
        => new((MouseButton)e.Button, e.Down, e.Clicks, new Vector2(e.X, e.Y));

    public static MouseWheelEventArgs MouseWheel(SDL.MouseWheelEvent e)
        => new(new Vector2(e.X, e.Y), new Vector2(e.MouseX, e.MouseY), (MouseWheelDirection)e.Direction);

    public static TextInputEventArgs TextInput(SDL.TextInputEvent e)
        => new(Marshal.PtrToStringUTF8(e.Text) ?? "");

    public static TextEditingEventArgs TextEditing(SDL.TextEditingEvent e)
        => new(Marshal.PtrToStringUTF8(e.Text) ?? "", e.Start, e.Length);
}

#endregion

using System.Collections.Immutable;
using System.Diagnostics;

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
    public async void Dispose()
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

                _disposeTcs.SetResult();
            }
        }
    }

    private readonly TaskCompletionSource _disposeTcs =
        new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The returned task completes once the window has been fully disposed and all resources have been released.
    /// </summary>
    public Task WaitForDisposeAsync() => _disposeTcs.Task;

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
    public PixelFormat PixelFormat
    {
        get
        {
            if (IsDisposed)
                return PixelFormat.Unknown;
            return (PixelFormat)SDL.GetWindowPixelFormat(_window);
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

    #region Heartbeat

    /// <summary>
    /// The <see cref="HeartBeatRate"/> determines how often the <see cref="HeartBeat"/> event is fired. 
    /// Setting it to <see cref="TimeSpan.Zero"/> will disable heartbeats.
    /// </summary>
    public TimeSpan HeartBeatRate 
    { 
        get; 
        set
        {                      
            if (value != field)
            {
                field = value;
                SetHeartBeatRate(value);
            }
        } 
    }

    private readonly object _heartBeatGate = new object();
    private int _heartBeatGeneration;   // incremented on every rate change    
    private CancellationTokenSource? _heartBeatCancellation;

    private void SetHeartBeatRate(TimeSpan rate)
    {
        lock (_heartBeatGate)
        {
            // Cancel any in-flight loop. We don't wait — old loops self-terminate.
            _heartBeatCancellation?.Cancel();
            _heartBeatCancellation?.Dispose();
            _heartBeatCancellation = null;

            var generation = ++_heartBeatGeneration;
            if (rate <= TimeSpan.Zero) 
                return;

            var cts = new CancellationTokenSource();
            _heartBeatCancellation = cts;
            _ = RunHeartbeatAsync(rate, generation, cts.Token);
        }
    }

    private async Task RunHeartbeatAsync(TimeSpan rate, int generation, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(rate);
        var startTs = Stopwatch.GetTimestamp();
        try
        {
            var lastBeatTime = startTs;

            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                if (IsDisposed) 
                    break;

                // If a newer loop has been started, abandon ours silently.
                if (Volatile.Read(ref _heartBeatGeneration) != generation) 
                    break;

                var nowTs = Stopwatch.GetTimestamp();
                var elapsedSinceStart = Stopwatch.GetElapsedTime(startTs, nowTs);
                var elapsedSinceLastBeat = Stopwatch.GetElapsedTime(lastBeatTime, nowTs);
                lastBeatTime = nowTs;

                OnHeartBeat(new HeartBeatEventArgs(elapsedSinceStart, elapsedSinceLastBeat));
            }
        }
        catch (OperationCanceledException) 
        { 
            /* expected on rate change / dispose */ 
        }
    }

    public event HeartBeatEventHandler? HeartBeat;

    protected virtual void OnHeartBeat(HeartBeatEventArgs args)
    {
        this.HeartBeat?.Invoke(this, args);
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
        // Idle -> Scheduled: queue a new render
        if (Interlocked.CompareExchange(ref _renderState, RenderState.Scheduled, RenderState.Idle) == RenderState.Idle)
        {
            Application.Current.Post(_ => DoRenderInternal(), null);
            return;
        }
        // Rendering -> Scheduled: ask the in-flight render to queue a follow-up frame
        Interlocked.CompareExchange(ref _renderState, RenderState.Scheduled, RenderState.Rendering);
        // Scheduled -> Scheduled: already coalesced, nothing to do
    }

    /// <summary>
    /// Upper bound on the per-frame elapsed time reported to <see cref="RenderingFrame"/>
    /// handlers. Long pauses (window minimized, debugger break, system sleep) would
    /// otherwise produce a single huge delta that teleports time-integrated state.
    /// Set to <see cref="TimeSpan.MaxValue"/> to disable clamping.
    /// </summary>
    public TimeSpan MaxFrameDelta { get; set; } = TimeSpan.FromMilliseconds(250);

    private void DoRenderInternal()
    {
        _renderState = RenderState.Rendering;

        var nowTs = Stopwatch.GetTimestamp();
        var elapsedSinceCreate    = Stopwatch.GetElapsedTime(_startTs, nowTs);
        var elapsedSinceLastFrame = Stopwatch.GetElapsedTime(_lastFrameTs, nowTs);
        _lastFrameTs = nowTs;

        // Clamp implausibly large per-frame deltas. ElapsedSinceWindowCreated is
        // intentionally NOT clamped — absolute time keeps advancing through pauses.
        var maxDelta = MaxFrameDelta;
        if (elapsedSinceLastFrame > maxDelta)
            elapsedSinceLastFrame = maxDelta;

        try
        {
            DoRenderFrame(elapsedSinceCreate, elapsedSinceLastFrame);
        }
        finally
        {
            // Rendering -> Idle if no one re-invalidated during the frame.
            // If state is Scheduled instead, the user called Invalidate(); post another.
            if (Interlocked.CompareExchange(ref _renderState, RenderState.Idle, RenderState.Rendering)
                != RenderState.Rendering)
            {
                Application.Current.Post(_ => DoRenderInternal(), null);
            }
        }
    }
    
    private readonly long _startTs = Stopwatch.GetTimestamp();
    private long _lastFrameTs = Stopwatch.GetTimestamp();

     /// <summary>
    /// Performs the per-frame rendering for this window. Implementations are
    /// invoked on the application thread.
    /// </summary>
    protected abstract void DoRenderFrame(TimeSpan elapsedSinceWindowCreated, TimeSpan elapsedSinceLastFrame);

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

public delegate void HeartBeatEventHandler(Window sender, HeartBeatEventArgs args);

public record struct HeartBeatEventArgs(TimeSpan ElapsedSinceStart, TimeSpan ElapsedSinceLastBeat);

public record struct WindowRenderEventArgs<TRenderer>(TimeSpan ElapsedSinceWindowCreated, TimeSpan ElapsedSinceLastFrame, TRenderer Renderer);
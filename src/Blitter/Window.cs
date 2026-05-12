using System.Collections.Immutable;
using System.Diagnostics;
using Blitter.Devices;
using Blitter.Events;
using Blitter.Utilities;

namespace Blitter;

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
        : this(DefaultSize(), flags)
    {
    }

    private Window((int Width, int Height) size, WindowFlags flags)
        : this(size.Width, size.Height, flags)
    {
    }

    // Half the primary display's usable bounds (i.e. excluding the
    // taskbar) so a default-sized window is comfortably visible on
    // any reasonable monitor. Falls back to 800x600 if SDL can't
    // report a primary display (rare; headless / no video init).
    private static (int Width, int Height) DefaultSize()
    {
        const int Fallback_W = 800, Fallback_H = 600;
        int w = Fallback_W, h = Fallback_H;
        try
        {
            // The window ctor body initializes the video subsystem,
            // but we need it up *before* that to query display bounds.
            // SDL refcounts so the ctor's later init call is a no-op.
            // Display + SDL calls run on the application thread.
            var app = Application.Start();
            app.Send(_ =>
            {
                if (!SDL.InitSubSystem(SDL.InitFlags.Video))
                    return;
                var bounds = Devices.Display.Primary.UsableBounds;
                int hw = (int)(bounds.Width / 2);
                int hh = (int)(bounds.Height / 2);
                if (hw > 0 && hh > 0)
                {
                    w = hw;
                    h = hh;
                }
            });
        }
        catch
        {
            // Fall through with whatever w/h we captured.
        }
        return (w, h);
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
    /// True if this window has been closed (disposed).
    /// </summary>
    public bool IsClosed => _window == 0;

    private void ThrowIfDisposed()
    {
        if (IsClosed)
            throw new InvalidOperationException("Window is closed");
    }

    // SDL window mutation/inspection must run on the application (event)
    // thread to be cross-platform safe — Cocoa enforces this strictly,
    // X11/Win32 are more forgiving but can race. These helpers marshal
    // through Application.Invoke (no-op when already on the app thread).
    private void OnApp(Action action)
    {
        if (IsClosed) return;
        Application.Current.Invoke(action);
    }

    private T OnApp<T>(Func<T> func, T closedDefault)
    {
        if (IsClosed) return closedDefault;
        return Application.Current.Invoke(func);
    }

    /// <summary>
    /// Disposes this window, releasing its resources.
    /// Automatically called by <see cref="Close"/>
    /// </summary>
    public void Dispose()
    {
        if (!IsClosed)
        {
            var id = Interlocked.Exchange(ref _window, 0);
            if (id != 0)
            {
                _renderTick?.Dispose();
                _renderTick = null;
                _heartBeatTick?.Dispose();
                _heartBeatTick = null;
                _frameAwaiter?.Dispose();
                _frameAwaiter = null;

                OnDispose();

                foreach (var resource in _resources)
                {
                    resource.Dispose();
                }

                SDL.DestroyWindow(id);

                _resources.Clear();

                Application.Current?.RemoveWindow(this);

                _closedTcs.TrySetResult();
            }
        }
    }

    /// <summary>
    /// Closes this window and disposes it.
    /// </summary>
    public void Close() => Dispose();

    private readonly TaskCompletionSource _closedTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// A task that completes when this window is closed.
    /// </summary>
    public Task ClosedTask => _closedTcs.Task;
    
    /// <summary>
    /// Asynchronously waits for this window to be closed.
    /// </summary>
    public Task WaitForCloseAsync(CancellationToken cancellationToken = default)
        => _closedTcs.Task.WaitAsync(cancellationToken);

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
            if (IsClosed)
                return Properties.Empty;
            if (_properties == null)
                _properties = new Properties(SDL.GetWindowProperties(_window));
            return _properties;
        }
    }

    /// <summary>
    /// The display that the window is currently on.
    /// </summary>
    public DisplayDevice Display
    {
        get
        {
            ThrowIfDisposed();
            var displayId = SDL.GetDisplayForWindow(_window);
            if (DisplayDevice.TryGetDisplay(displayId, out var display))
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
            if (IsClosed || value == null)
                return;
            _icon = value;
            OnApp(() => SDL.SetWindowIcon(_window, value._imageId));
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
            return OnApp(
                () => (WindowFlags)((ulong)SDL.GetWindowFlags(_window) & creationMask),
                WindowFlags.None);
        }
    }

    /// <summary>
    /// True if the window can currently receive keyboard focus.
    /// </summary>
    public bool Focusable
    {
        get => OnApp(() => (SDL.GetWindowFlags(_window) & SDL.WindowFlags.NotFocusable) == 0, false);
        set => OnApp(() => SDL.SetWindowFocusable(_window, value));
    }

    /// <summary>
    /// The aspect ratio of the window.
    /// </summary>
    public (float Min, float Max) AspectRatio
    {
        get => OnApp(() =>
        {
            SDL.GetWindowAspectRatio(_window, out float minAspect, out float maxAspect);
            return (minAspect, maxAspect);
        }, (0f, 0f));
        set => OnApp(() => SDL.SetWindowAspectRatio(_window, value.Min, value.Max));
    }

    /// <summary>
    /// True if the window has a border.
    /// </summary>
    public bool Bordered
    {
        get => OnApp(() => (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Borderless) == 0, false);
        set => OnApp(() => SDL.SetWindowBordered(_window, value));
    }

    /// <summary>True if the window can be resized by the user.</summary>
    public bool Resizable
    {
        get => OnApp(() => (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Resizable) != 0, false);
        set => OnApp(() => SDL.SetWindowResizable(_window, value));
    }

    /// <summary>True if the window stays above all other windows.</summary>
    public bool AlwaysOnTop
    {
        get => OnApp(() => (SDL.GetWindowFlags(_window) & SDL.WindowFlags.AlwaysOnTop) != 0, false);
        set => OnApp(() => SDL.SetWindowAlwaysOnTop(_window, value));
    }

    /// <summary>True if the window has captured the mouse cursor.</summary>
    public bool MouseGrabbed
    {
        get => OnApp(() => SDL.GetWindowMouseGrab(_window), false);
        set => OnApp(() => SDL.SetWindowMouseGrab(_window, value));
    }

    /// <summary>True if the window has grabbed keyboard input.</summary>
    public bool KeyboardGrabbed
    {
        get => OnApp(() => SDL.GetWindowKeyboardGrab(_window), false);
        set => OnApp(() => SDL.SetWindowKeyboardGrab(_window, value));
    }

    /// <summary>True if the window is in relative mouse mode.</summary>
    public bool RelativeMouseMode
    {
        get => OnApp(() => SDL.GetWindowRelativeMouseMode(_window), false);
        set => OnApp(() => SDL.SetWindowRelativeMouseMode(_window, value));
    }

    /// <summary>True if the window is currently modal to its parent.</summary>
    public bool Modal
    {
        get => OnApp(() => (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Modal) != 0, false);
        set => OnApp(() => SDL.SetWindowModal(_window, value));
    }

    /// <summary>True if the window is currently hidden.</summary>
    public bool IsHidden
        => OnApp(() => (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Hidden) != 0, false);

    /// <summary>True if the window is currently minimized.</summary>
    public bool IsMinimized
        => OnApp(() => (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Minimized) != 0, false);

    /// <summary>True if the window is currently maximized.</summary>
    public bool IsMaximized
        => OnApp(() => (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Maximized) != 0, false);

    /// <summary>True if the window is currently occluded.</summary>
    public bool IsOccluded
        => OnApp(() => (SDL.GetWindowFlags(_window) & SDL.WindowFlags.Occluded) != 0, false);

    /// <summary>True if the window currently has keyboard input focus.</summary>
    public bool HasInputFocus
        => OnApp(() => (SDL.GetWindowFlags(_window) & SDL.WindowFlags.InputFocus) != 0, false);

    /// <summary>True if the window currently has mouse focus.</summary>
    public bool HasMouseFocus
        => OnApp(() => (SDL.GetWindowFlags(_window) & SDL.WindowFlags.MouseFocus) != 0, false);

    /// <summary>
    /// The size of the window borders in pixels.
    /// </summary>
    public (int Top, int Left, int Bottom, int Right) BorderSize
    {
        get => OnApp(() =>
        {
            SDL.GetWindowBordersSize(_window, out int top, out int left, out int bottom, out int right);
            return (top, left, bottom, right);
        }, (0, 0, 0, 0));
    }

    /// <summary>
    /// The scale factor for the window's display.
    /// </summary>
    public float DisplayScale
        => OnApp(() => SDL.GetWindowDisplayScale(_window), 1.0f);

    /// <summary>
    /// True if the window is in full-screen mode.
    /// </summary>
    public bool FullScreen
    {
        get => OnApp(() => SDL.GetWindowFullscreenMode(_window) != null, false);
        set => OnApp(() => SDL.SetWindowFullscreen(_window, value));
    }

    /// <summary>
    /// The display mode of the window when in full-screen mode.
    /// </summary>
    public DisplayMode FullScreenMode
    {
        get => OnApp(() => SDL.GetWindowFullscreenMode(_window) is { } mode
            ? new DisplayMode(mode)
            : default, default);
        set => OnApp(() => SDL.SetWindowFullscreenMode(_window, value._mode));
    }

    /// <summary>
    /// The minimum size of the window (when it is minimized).
    /// </summary>
    public (int Width, int Height) MinimumSize
    {
        get => OnApp(() =>
        {
            SDL.GetWindowMinimumSize(_window, out int width, out int height);
            return (width, height);
        }, (0, 0));
        set => OnApp(() => SDL.SetWindowMinimumSize(_window, value.Width, value.Height));
    }

    /// <summary>
    /// The maximum size of the window (when it is maximized).
    /// </summary>
    public (int Width, int Height) MaximumSize
    {
        get => OnApp(() =>
        {
            SDL.GetWindowMaximumSize(_window, out int width, out int height);
            return (width, height);
        }, (0, 0));
        set => OnApp(() => SDL.SetWindowMaximumSize(_window, value.Width, value.Height));
    }

    /// <summary>
    /// The pixel density of the window.
    /// </summary>
    public float PixelDensity
        => OnApp(() => SDL.GetWindowPixelDensity(_window), 1f);

    /// <summary>
    /// The pixel format of the window.
    /// </summary>
    public PixelFormat PixelFormat
        => OnApp(() => (PixelFormat)SDL.GetWindowPixelFormat(_window), PixelFormat.Unknown);

    /// <summary>
    /// The position of the window in the multi-display space.
    /// </summary>
    public (int X, int Y) Position
    {
        get => OnApp(() =>
        {
            SDL.GetWindowPosition(_window, out int x, out int y);
            return (x, y);
        }, (0, 0));
        set => OnApp(() => SDL.SetWindowPosition(_window, value.X, value.Y));
    }

    /// <summary>
    /// The size of the window in pixels.
    /// </summary>
    public (int Width, int Height) Size
    {
        get => OnApp(() =>
        {
            SDL.GetWindowSize(_window, out int width, out int height);
            return (width, height);
        }, (0, 0));
        set => OnApp(() => SDL.SetWindowSize(_window, value.Width, value.Height));
    }

    /// <summary>
    /// The title of the window.
    /// </summary>
    public string Title
    {
        get => OnApp(() => SDL.GetWindowTitle(_window), "");
        set => OnApp(() => SDL.SetWindowTitle(_window, value));
    }
    #endregion

    #region Window state methods

    /// <summary>Shows the window if it was hidden.</summary>
    public void Show() => OnApp(() => SDL.ShowWindow(_window));

    /// <summary>Hides the window without destroying it.</summary>
    public void Hide() => OnApp(() => SDL.HideWindow(_window));

    /// <summary>Minimizes the window to the taskbar/dock.</summary>
    public void Minimize() => OnApp(() => SDL.MinimizeWindow(_window));

    /// <summary>Maximizes the window to fill its display.</summary>
    public void Maximize() => OnApp(() => SDL.MaximizeWindow(_window));

    /// <summary>
    /// Restores the window from a minimized or maximized state to its
    /// previous size and position.
    /// </summary>
    public void Restore() => OnApp(() => SDL.RestoreWindow(_window));

    /// <summary>Brings the window above other windows and gives it focus.</summary>
    public void Raise() => OnApp(() => SDL.RaiseWindow(_window));

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
    private IDisposable? _heartBeatTick;
    private long _heartBeatStartTs;
    private long _heartBeatLastTs;

    private void SetHeartBeatRate(TimeSpan rate)
    {
        lock (_heartBeatGate)
        {
            _heartBeatTick?.Dispose();
            _heartBeatTick = null;

            if (rate <= TimeSpan.Zero)
                return;

            _heartBeatStartTs = Stopwatch.GetTimestamp();
            _heartBeatLastTs = _heartBeatStartTs;
            _heartBeatTick = Application.Current.ScheduleTick(rate, OnHeartBeatTick);
        }
    }

    private void OnHeartBeatTick()
    {
        if (IsClosed)
        {
            // Window was closed while the tick was registered; tear it down.
            _heartBeatTick?.Dispose();
            _heartBeatTick = null;
            return;
        }

        var nowTs = Stopwatch.GetTimestamp();
        var elapsedSinceStart = Stopwatch.GetElapsedTime(_heartBeatStartTs, nowTs);
        var elapsedSinceLastBeat = Stopwatch.GetElapsedTime(_heartBeatLastTs, nowTs);
        _heartBeatLastTs = nowTs;

        OnHeartBeat(new HeartBeatEventArgs(elapsedSinceStart, elapsedSinceLastBeat));
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
    /// Subclasses forward changes to their renderer.
    /// </summary>
    public virtual Color BackgroundColor { get; set; }

    /// <summary>
    /// If set to a value other than <see cref="Key.Unknown"/>, the window
    /// disposes itself when this key is pressed. The <see cref="KeyDown"/>
    /// event is still raised first, so handlers can observe the keypress
    /// before the window goes away.
    /// </summary>
    public Key CloseKey { get; set; } = Key.Unknown;

    /// <summary>
    /// Minimum time between renders. The window polls for invalidations
    /// at this rate; multiple <see cref="Invalidate"/> calls within one
    /// interval coalesce into a single render. Defaults to ~16.67 ms
    /// (60 renders per second). Set to <see cref="TimeSpan.Zero"/> to
    /// remove the cap and render as fast as possible.
    /// </summary>
    public TimeSpan MinRenderInterval
    {
        get => _minRenderInterval;
        set
        {
            if (value < TimeSpan.Zero)
                value = TimeSpan.Zero;
            if (value == _minRenderInterval)
                return;
            _minRenderInterval = value;

            // If the render tick is already registered, re-register it
            // at the new period so the next tick fires at the updated
            // cadence.
            if (_renderTick is not null)
            {
                _renderTick.Dispose();
                _renderTick = Application.Current.ScheduleTick(_minRenderInterval, OnRenderTick);
            }
        }
    }

    private TimeSpan _minRenderInterval = TimeSpan.FromSeconds(1.0 / 60);
    private int _invalidationRequested;
    private IDisposable? _renderTick;

    /// <summary>
    /// Invalidates the window, requesting a new render. The render runs
    /// on the next frame tick (see <see cref="MinRenderInterval"/>); multiple
    /// invalidations between ticks coalesce into a single render.
    /// </summary>
    public void Invalidate()
    {
        if (IsClosed)
            return;

        Interlocked.Exchange(ref _invalidationRequested, 1);
        EnsureRenderLoopStarted();
    }

    private void EnsureRenderLoopStarted()
    {
        // Register the render tick on first invalidation. The tick runs
        // on the application thread; while idle (no invalidations) it
        // just polls the request flag at the configured cadence.
        if (_renderTick is not null)
            return;
        _renderTick = Application.Current.ScheduleTick(_minRenderInterval, OnRenderTick);
    }

    private void OnRenderTick()
    {
        if (IsClosed)
        {
            _renderTick?.Dispose();
            _renderTick = null;
            return;
        }

        if (Interlocked.Exchange(ref _invalidationRequested, 0) == 1)
        {
            try
            {
                RenderFrame(_renderingEventBody ??= () => RaiseRenderingEvent());
            }
            catch
            {
                // Swallow handler exceptions so they don't tear down
                // the render loop. Specific failures should be
                // diagnosed by the handlers themselves.
            }
        }
    }

    private Action? _renderingEventBody;

    private PeriodicAwaiter EnsureFrameAwaiter()
    {
        // (Re)create the awaiter when MinRenderInterval changes so the
        // tick cadence always matches the current setting.
        var awaiter = _frameAwaiter;
        if (awaiter is null || awaiter.Period != _minRenderInterval)
        {
            awaiter?.Dispose();
            awaiter = new PeriodicAwaiter(_minRenderInterval);
            _frameAwaiter = awaiter;
        }
        return awaiter;
    }

    private PeriodicAwaiter? _frameAwaiter;

    /// <summary>
    /// Animates the window by repeatedly calling <paramref name="renderFrame"/> on each frame tick
    /// until <paramref name="shouldContinue"/> returns false, the window is closed, or <paramref name="cancellationToken"/> fires.
    /// </summary>
    public Task RunAsync(Func<bool> shouldContinue, Action renderFrame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shouldContinue);
        ArgumentNullException.ThrowIfNull(renderFrame);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                while (!IsClosed && !cancellationToken.IsCancellationRequested && shouldContinue())
                {
                    RenderFrame(renderFrame);
                    if (IsClosed || cancellationToken.IsCancellationRequested)
                        break;
                    EnsureFrameAwaiter().Wait(cancellationToken);
                }
                tcs.TrySetResult();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = $"Blitter.RenderLoop ({Title})",
        };
        thread.Start();
        return tcs.Task;
    }

    /// <summary>
    /// Animates the window by repeatedly calling <paramref name="renderFrame"/> on each frame tick
    /// until the window is closed, or <paramref name="cancellationToken"/> fires.
    /// </summary>
    public Task RunAsync(Action renderFrame, CancellationToken cancellationToken = default)
        => RunAsync(static () => true, renderFrame, cancellationToken);

    /// <summary>
    /// Invokes <paramref name="body"/> in a frame-rendering context:
    /// any draws queued on the window's renderer are flushed once
    /// <paramref name="body"/> returns, and stray <c>Render()</c> calls
    /// from inside it are suppressed so they don't double-present.
    /// </summary>
    protected abstract void RenderFrame(Action body);

    /// <summary>
    /// Raises the <c>Rendering</c> event on derived classes; called
    /// automatically by the event-driven render loop.
    /// </summary>
    protected abstract void RaiseRenderingEvent();

    private FrameInput? _input;

    /// <summary>
    /// Per-window input snapshot. Advances automatically at the start
    /// of each rendered frame; query <c>WasJustPressed</c>, <c>Direction</c>,
    /// <c>MouseDelta</c> etc. from inside the render body. Lazily created
    /// on first access.
    /// </summary>
    public FrameInput Input => _input ??= new FrameInput();

    /// <summary>
    /// Advances the per-window <see cref="Input"/> snapshot if it has
    /// been created. Called by <see cref="RenderFrame"/> overrides
    /// before user code runs.
    /// </summary>
    protected void AdvanceInput() => _input?.Update();

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
                DisplayDevice.TryGetDisplay((uint)e.Window.Data1, out var newDisplay);
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
    protected virtual void OnKeyDown(Window window, KeyEventArgs e)
    {
        this.KeyDown?.Invoke(window, e);
        if (this.CloseKey != Key.Unknown && e.Key == this.CloseKey && !this.IsClosed)
            this.Close();
    }

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

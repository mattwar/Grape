using System.Collections.Immutable;
using Nito.AsyncEx;

using Blitter.Devices;
using Blitter.Events;

namespace Blitter;

/// <summary>
/// A SDL Application.
/// </summary>
public class Application : IDisposable
{
    private bool _disposed;
    private ImmutableList<IDisposable> _resources = ImmutableList<IDisposable>.Empty;

    public Application()
    {
        if (_current != null)
            throw new InvalidOperationException("An instance of Application already exists.");
        // Initialize only the Events subsystem here. Other subsystems are
        // brought up lazily by the types that need them (Window -> Video,
        // Audio -> Audio, Gamepad -> Gamepad, etc). SDL's subsystem init is
        // ref-counted, so calling InitSubSystem from many places is safe.
        if (!SDL.Init(SDL.InitFlags.Events))
            throw new InvalidOperationException($"Failed to initialize SDL: {SDL.GetError()}");
        _current = this;
        this.Thread = Thread.CurrentThread;
    }

    /// <summary>
    /// The current running application. Accessing this property will start
    /// the application if one is not already running. Subsystems beyond
    /// the basic event loop are initialized lazily on first use of the
    /// types that need them (creating a <see cref="Window"/> brings up
    /// video, touching <see cref="Audio"/> brings up audio, and so on).
    /// </summary>
    public static Application Current => _current ?? Start();

    private static Application? _current;
    private static readonly object _startLock = new();

    /// <summary>
    /// The thread the application was created on.
    /// </summary>
    public Thread Thread { get; }

    /// <summary>
    /// True if the application has been shut down (disposed).
    /// </summary>
    public bool IsShutdown => _disposed;

    private readonly TaskCompletionSource _shutdownTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// A task that completes when this application has shut down.
    /// </summary>
    public Task ShutdownTask => _shutdownTcs.Task;

    /// <summary>
    /// Asynchronously waits for this application to shut down.
    /// </summary>
    public Task WaitForShutdownAsync(CancellationToken cancellationToken = default)
        => _shutdownTcs.Task.WaitAsync(cancellationToken);

    /// <summary>
    /// Shuts down the application, closing all open windows
    /// and automatically disposing it and all its resources.
    /// </summary>
    public void Shutdown() => Dispose();

    /// <summary>
    /// Disposes the application and any related resources. Equivalent
    /// to <see cref="Shutdown"/>; prefer <see cref="Shutdown"/> for
    /// user-facing shutdown logic.
    /// </summary>
    public void Dispose()
    {
        if (IsShutdown)
            return;

        if (Interlocked.CompareExchange(ref _disposed, true, false) == false)
        {
            foreach (var window in _windows)
            {
                window.Dispose();
            }

            foreach (var resource in _resources)
            {
                resource.Dispose();
            }

            _windows = ImmutableList<Window>.Empty;
            _resources = ImmutableList<IDisposable>.Empty;

            if (_current == this)
                _current = null;

            SDL.Quit();

            _shutdownTcs.TrySetResult();
        }
    }

    #region Windows and Resources
    private ImmutableList<Window> _windows = ImmutableList<Window>.Empty;

    /// <summary>
    /// The windows open in the application.
    /// </summary>
    public IReadOnlyList<Window> Windows => _windows;

    /// <summary>
    /// Adds a window to the applications window list.
    /// </summary>
    internal void AddWindow(Window window)
    {
        ImmutableInterlocked.Update(ref _windows, list => list.Add(window));
        AddResource(window);
    }

    /// <summary>
    /// Removes a window from the applications window list.
    /// </summary>
    internal void RemoveWindow(Window window)
    {
        ImmutableInterlocked.Update(ref _windows, list => list.Remove(window));
        RemoveResource(window);

        // When the last window closes, ask the event loop to exit so the
        // foreground app thread can terminate and the process can shut down.
        // Callers that want a headless application should create the
        // Application explicitly and call Dispose() themselves.
        if (_windows.IsEmpty)
            _quitRequested = true;
    }

    private volatile bool _quitRequested;

    /// <summary>
    /// Gets the window with the specified ID.
    /// </summary>
    private Window GetWindow(uint windowId)
    {
        return _windows.FirstOrDefault(w => w.EventId == windowId)
               ?? throw new ArgumentException($"Window with ID {windowId} not found.", nameof(windowId));
    }

    internal void AddResource(IDisposable resource)
    {
        ImmutableInterlocked.Update(ref _resources, list => list.Add(resource));
    }

    internal void RemoveResource(IDisposable resource)
    {
        ImmutableInterlocked.Update(ref _resources, list => list.Remove(resource));
    }

    /// <summary>
    /// The displays available to the application.
    /// </summary>
    public ImmutableList<DisplayDevice> Displays => Display.Displays;
    #endregion

    #region Event Loop and threading
    /// <summary>
    /// Starts the application event loop running on a new thread.
    /// </summary>
    public static Application Start()
    {
        // Serialize start so concurrent Application.Current accesses
        // (e.g. parallel xUnit test classes touching Image/Model) don't
        // both spawn an app thread and race the singleton check in the
        // constructor.
        lock (_startLock)
        {
            var application = _current;

            if (application == null)
            {
                var tcs = new TaskCompletionSource<Application>();
                var appThread = new Thread(_ =>
                {
                    application = new Application();
                    application.Run(() => tcs.SetResult(application));
                    application.Dispose();
                })
                {
                    // Background so a headless caller (e.g. Image.Render3D
                    // with no window ever opened) doesn't keep the process
                    // alive after the main thread exits. Windowed apps still
                    // shut down cleanly via _quitRequested when the last
                    // window closes; this only changes the no-window case.
                    IsBackground = true,
                    Name = "Blitter.Application",
                };
                appThread.Start();
                application = tcs.Task.Result;
            }

            return application;
        }
    }

    /// <summary>
    /// True if the application event loop is running.
    /// </summary>
    private bool _running;

    /// <summary>
    /// Runs the event loop of the application.
    /// </summary>
    public void Run(Action? onStart = null)
    {       
        if (Interlocked.CompareExchange(ref _running, true, false) == false)
        {
            AsyncContext.Run(() =>
            {
                _context = SynchronizationContext.Current;
                onStart?.Invoke();
                return RunEventLoopAsync();
            });
            _running = false;
        }
    }

    private SynchronizationContext? _context;

    private async Task RunEventLoopAsync()
    {
        while (!IsShutdown && !_quitRequested)
        {
            while (SDL.PollEvent(out var e))
            {
                if (e.Type == (uint)SDL.EventType.Quit)
                    goto exit;
   
                DispatchEvent(e);
            }

            await Task.Delay(16);
        }

    exit:
        _context = null;
    }

    /// <summary>
    /// Executes the callback asynchronously on the application's main thread.
    /// </summary>
    public void Post(SendOrPostCallback callback, object? state = null)
    {
        if (_context is { } context)
        {
            // post to the application's synchronization context
            context.Post(callback, state);
        }
        else
        {
            // application synchronization context is not available, so just invoke the callback
            callback(state);
        }
    }

    /// <summary>
    /// Executes the callback asynchronously on the application's main thread.
    /// </summary>
    public Task PostAsync(SendOrPostCallback callback, object? state = null)
    {
        if (_context is { } context)
        {
            // post to the application's synchronization context
            var tcs = new TaskCompletionSource();
            context.Post(_state => { callback(_state); tcs.SetResult(); }, state);
            return tcs.Task;
        }
        else
        {
            // application synchronization context is not available, so just invoke the callback
            callback(state);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Executes the callback synchronously on the application's main thread.
    /// </summary>
    public void Send(SendOrPostCallback callback, object? state = null)
    {
        if (this.Thread == Thread.CurrentThread)
        {
            // this may jump the queue, but we're already on the right thread
            callback(state);
        }
        else if (_context is { } context)
        {
            // send to the application's synchronization context
            context.Send(callback, state);
        }
        else
        {
            // application synchronization context is not available, so just invoke the callback
            callback(state);
        }
    }
    #endregion

    #region events

    private void DispatchEvent(SDL.Event e)
    {
        switch ((SDL.EventType)e.Type)
        {
            // application events
            case SDL.EventType.Quit:
                this.OnQuitting(default);
                break;
            case SDL.EventType.Terminating:
                this.OnTerminating(default);
                break;
            case SDL.EventType.LowMemory:
                this.OnLowMemory(default);
                break;
            case SDL.EventType.WillEnterBackground:
                this.OnEnteringBackground(default);
                break;
            case SDL.EventType.DidEnterBackground:
                this.OnEnteredBackground(default);
                break;
            case SDL.EventType.WillEnterForeground:
                this.OnEnteringForeground(default);
                break;
            case SDL.EventType.DidEnterForeground:
                this.OnEnteredForeground(default);
                break;
            case SDL.EventType.LocaleChanged:
                this.OnLocaleChanged(default);
                break;

            // keyboard events
            case SDL.EventType.KeyDown:
                this.DispatchWindowEvent(e.Key.WindowID, e);
                break;
            case SDL.EventType.KeyUp:
                this.DispatchWindowEvent(e.Key.WindowID, e);
                break;
            case SDL.EventType.TextEditing:
                this.DispatchWindowEvent(e.Edit.WindowID, e);
                break;
            case SDL.EventType.TextInput:
                this.DispatchWindowEvent(e.Text.WindowID, e);
                break;
            case SDL.EventType.KeymapChanged:
            case SDL.EventType.KeyboardAdded:
            case SDL.EventType.KeyboardRemoved:
            case SDL.EventType.TextEditingCandidates:
                break;

            // window events
            case SDL.EventType.WindowCloseRequested:
            case SDL.EventType.WindowDestroyed:
            case SDL.EventType.WindowDisplayChanged:
            case SDL.EventType.WindowDisplayScaleChanged:
            case SDL.EventType.WindowEnterFullscreen:
            case SDL.EventType.WindowLeaveFullscreen:
            case SDL.EventType.WindowFocusGained:
            case SDL.EventType.WindowFocusLost:
            case SDL.EventType.WindowHidden:
            case SDL.EventType.WindowShown:
            case SDL.EventType.WindowExposed:
            case SDL.EventType.WindowOccluded:
            case SDL.EventType.WindowMaximized:
            case SDL.EventType.WindowMinimized:
            case SDL.EventType.WindowResized:
            case SDL.EventType.WindowRestored:
            case SDL.EventType.WindowMouseEnter:
            case SDL.EventType.WindowMouseLeave:
            case SDL.EventType.WindowMoved:
            case SDL.EventType.WindowPixelSizeChanged:
            case SDL.EventType.WindowSafeAreaChanged:
            case SDL.EventType.WindowHDRStateChanged:
            case SDL.EventType.WindowHitTest:
            case SDL.EventType.WindowICCProfChanged:
            case SDL.EventType.WindowMetalViewResized:
                DispatchWindowEvent(e.Window.WindowID, e);
                break;

            case SDL.EventType.MouseAdded:
                this.OnMouseDeviceAdded(new MouseDeviceEventArgs(e.MDevice.Which));
                break;
            case SDL.EventType.MouseRemoved:
                this.OnMouseDeviceRemoved(new MouseDeviceEventArgs(e.MDevice.Which));
                break;
            case SDL.EventType.MouseButtonDown:
                this.DispatchWindowEvent(e.Button.WindowID, e);
                break;
            case SDL.EventType.MouseButtonUp:
                this.DispatchWindowEvent(e.Button.WindowID, e);
                break;
            case SDL.EventType.MouseMotion:
                this.DispatchWindowEvent(e.Motion.WindowID, e);
                break;
            case SDL.EventType.MouseWheel:
                this.DispatchWindowEvent(e.Wheel.WindowID, e);
                break;
        }
    }

    private void DispatchWindowEvent(uint windowId, SDL.Event e)
    {
        var window = GetWindow(windowId);
        window.DispatchEvent(e);

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

    #region Application Events
    /// <summary>Raised when the user requests the application to quit.</summary>
    public event ApplicationEventHandler<QuittingEventArgs>? Quitting;
    protected virtual void OnQuitting(QuittingEventArgs e) { this.Quitting?.Invoke(this, e); }

    /// <summary>Raised when the OS is terminating the application (mobile).</summary>
    public event ApplicationEventHandler<TerminatingEventArgs>? Terminating;
    protected virtual void OnTerminating(TerminatingEventArgs e) { this.Terminating?.Invoke(this, e); }

    /// <summary>Raised when the OS reports low memory (mobile).</summary>
    public event ApplicationEventHandler<LowMemoryEventArgs>? LowMemory;
    protected virtual void OnLowMemory(LowMemoryEventArgs e) { this.LowMemory?.Invoke(this, e); }

    /// <summary>Raised when the application is about to enter the background.</summary>
    public event ApplicationEventHandler<EnteringBackgroundEventArgs>? EnteringBackground;
    protected virtual void OnEnteringBackground(EnteringBackgroundEventArgs e) { this.EnteringBackground?.Invoke(this, e); }

    /// <summary>Raised after the application has entered the background.</summary>
    public event ApplicationEventHandler<EnteredBackgroundEventArgs>? EnteredBackground;
    protected virtual void OnEnteredBackground(EnteredBackgroundEventArgs e) { this.EnteredBackground?.Invoke(this, e); }

    /// <summary>Raised when the application is about to enter the foreground.</summary>
    public event ApplicationEventHandler<EnteringForegroundEventArgs>? EnteringForeground;
    protected virtual void OnEnteringForeground(EnteringForegroundEventArgs e) { this.EnteringForeground?.Invoke(this, e); }

    /// <summary>Raised after the application has entered the foreground.</summary>
    public event ApplicationEventHandler<EnteredForegroundEventArgs>? EnteredForeground;
    protected virtual void OnEnteredForeground(EnteredForegroundEventArgs e) { this.EnteredForeground?.Invoke(this, e); }

    /// <summary>Raised when the system locale has changed.</summary>
    public event ApplicationEventHandler<LocaleChangedEventArgs>? LocaleChanged;
    protected virtual void OnLocaleChanged(LocaleChangedEventArgs e) { this.LocaleChanged?.Invoke(this, e); }
    #endregion

    #region Keyboard/Text Events
    public event ApplicationWindowEventHandler<KeyEventArgs>? KeyDown;
    protected virtual void OnKeyDown(Window window, KeyEventArgs e) { this.KeyDown?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<KeyEventArgs>? KeyUp;
    protected virtual void OnKeyUp(Window window, KeyEventArgs e) { this.KeyUp?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<TextEditingEventArgs>? TextEditing;
    protected virtual void OnTextEditing(Window window, TextEditingEventArgs e) { this.TextEditing?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<TextInputEventArgs>? TextInput;
    protected virtual void OnTextInput(Window window, TextInputEventArgs e) { this.TextInput?.Invoke(this, window, e); }
    #endregion

    #region Window Events
    public event ApplicationWindowEventHandler<WindowCloseRequestedEventArgs>? WindowCloseRequested;
    protected virtual void OnWindowCloseRequested(Window window, WindowCloseRequestedEventArgs e) { this.WindowCloseRequested?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowDestroyedEventArgs>? WindowDestroyed;
    protected virtual void OnWindowDestroyed(Window window, WindowDestroyedEventArgs e) { this.WindowDestroyed?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowDisplayChangedEventArgs>? WindowDisplayChanged;
    protected virtual void OnWindowDisplayChanged(Window window, WindowDisplayChangedEventArgs e) { this.WindowDisplayChanged?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowDisplayScaleChangedEventArgs>? WindowDisplayScaleChanged;
    protected virtual void OnWindowDisplayScaleChanged(Window window, WindowDisplayScaleChangedEventArgs e) { this.WindowDisplayScaleChanged?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowEnterFullscreenEventArgs>? WindowEnterFullscreen;
    protected virtual void OnWindowEnterFullscreen(Window window, WindowEnterFullscreenEventArgs e) { this.WindowEnterFullscreen?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowLeaveFullscreenEventArgs>? WindowLeaveFullscreen;
    protected virtual void OnWindowLeaveFullscreen(Window window, WindowLeaveFullscreenEventArgs e) { this.WindowLeaveFullscreen?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowFocusGainedEventArgs>? WindowFocusGained;
    protected virtual void OnWindowFocusGained(Window window, WindowFocusGainedEventArgs e) { this.WindowFocusGained?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowFocusLostEventArgs>? WindowFocusLost;
    protected virtual void OnWindowFocusLost(Window window, WindowFocusLostEventArgs e) { this.WindowFocusLost?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowHiddenEventArgs>? WindowHidden;
    protected virtual void OnWindowHidden(Window window, WindowHiddenEventArgs e) { this.WindowHidden?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowShownEventArgs>? WindowShown;
    protected virtual void OnWindowShown(Window window, WindowShownEventArgs e) { this.WindowShown?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowExposedEventArgs>? WindowExposed;
    protected virtual void OnWindowExposed(Window window, WindowExposedEventArgs e) { this.WindowExposed?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowOccludedEventArgs>? WindowOccluded;
    protected virtual void OnWindowOccluded(Window window, WindowOccludedEventArgs e) { this.WindowOccluded?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowMaximizedEventArgs>? WindowMaximized;
    protected virtual void OnWindowMaximized(Window window, WindowMaximizedEventArgs e) { this.WindowMaximized?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowMinimizedEventArgs>? WindowMinimized;
    protected virtual void OnWindowMinimized(Window window, WindowMinimizedEventArgs e) { this.WindowMinimized?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowResizedEventArgs>? WindowResized;
    protected virtual void OnWindowResized(Window window, WindowResizedEventArgs e) { this.WindowResized?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowRestoredEventArgs>? WindowRestored;
    protected virtual void OnWindowRestored(Window window, WindowRestoredEventArgs e) { this.WindowRestored?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowMouseEnterEventArgs>? WindowMouseEnter;
    protected virtual void OnWindowMouseEnter(Window window, WindowMouseEnterEventArgs e) { this.WindowMouseEnter?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowMouseLeaveEventArgs>? WindowMouseLeave;
    protected virtual void OnWindowMouseLeave(Window window, WindowMouseLeaveEventArgs e) { this.WindowMouseLeave?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowMovedEventArgs>? WindowMoved;
    protected virtual void OnWindowMoved(Window window, WindowMovedEventArgs e) { this.WindowMoved?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowPixelSizeChangedEventArgs>? WindowPixelSizeChanged;
    protected virtual void OnWindowPixelSizeChanged(Window window, WindowPixelSizeChangedEventArgs e) { this.WindowPixelSizeChanged?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowSafeAreaChangedEventArgs>? WindowSafeAreaChanged;
    protected virtual void OnWindowSafeAreaChanged(Window window, WindowSafeAreaChangedEventArgs e) { this.WindowSafeAreaChanged?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowHDRStateChangedEventArgs>? WindowHDRStateChanged;
    protected virtual void OnWindowHDRStateChanged(Window window, WindowHDRStateChangedEventArgs e) { this.WindowHDRStateChanged?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowHitTestEventArgs>? WindowHitTest;
    protected virtual void OnWindowHitTest(Window window, WindowHitTestEventArgs e) { this.WindowHitTest?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowICCProfChangedEventArgs>? WindowICCProfChanged;
    protected virtual void OnWindowICCProfChanged(Window window, WindowICCProfChangedEventArgs e) { this.WindowICCProfChanged?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<WindowMetalViewResizedEventArgs>? WindowMetalViewResized;
    protected virtual void OnWindowMetalViewResized(Window window, WindowMetalViewResizedEventArgs e) { this.WindowMetalViewResized?.Invoke(this, window, e); }
    #endregion

    #region Mouse Events

    public event ApplicationEventHandler<MouseDeviceEventArgs>? MouseDeviceAdded;
    protected virtual void OnMouseDeviceAdded(MouseDeviceEventArgs e) { this.MouseDeviceAdded?.Invoke(this, e); }

    public event ApplicationEventHandler<MouseDeviceEventArgs>? MouseDeviceRemoved;
    protected virtual void OnMouseDeviceRemoved(MouseDeviceEventArgs e) { this.MouseDeviceRemoved?.Invoke(this, e); }

    public event ApplicationWindowEventHandler<MouseMoveEventArgs>? MouseMotion;
    protected virtual void OnMouseMotion(Window window, MouseMoveEventArgs e) { this.MouseMotion?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<MouseButtonEventArgs>? MouseButtonDown;
    protected virtual void OnMouseButtonDown(Window window, MouseButtonEventArgs e) { this.MouseButtonDown?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<MouseButtonEventArgs>? MouseButtonUp;
    protected virtual void OnMouseButtonUp(Window window, MouseButtonEventArgs e) { this.MouseButtonUp?.Invoke(this, window, e); }

    public event ApplicationWindowEventHandler<MouseWheelEventArgs>? MouseWheel;
    protected virtual void OnMouseWheel(Window window, MouseWheelEventArgs e) { this.MouseWheel?.Invoke(this, window, e); }
    #endregion

    #endregion
}

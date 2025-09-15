using System.Collections.Immutable;
using Nito.AsyncEx;

namespace SDL3.Model;

/// <summary>
/// A SDL Application.
/// </summary>
public class Application : IDisposable
{
    private bool _disposed;
    private ImmutableList<IDisposable> _resources = ImmutableList<IDisposable>.Empty;

    private const SDL.InitFlags DefaultFlags = SDL.InitFlags.Video | SDL.InitFlags.Audio;

    public Application(SDL.InitFlags flags = DefaultFlags)
    {
        if (Current != null)
            throw new InvalidOperationException("An instance of Application already exists.");
        if (!SDL.Init(flags))
            throw new InvalidOperationException($"Failed to initialize SDL: {SDL.GetError()}");
        Current = this;
        this.Thread = Thread.CurrentThread;
    }

    /// <summary>
    /// The current running application.
    /// </summary>
    public static Application Current { get; private set; } = null!;

    /// <summary>
    /// The thread the application was created on.
    /// </summary>
    public Thread Thread { get; }

    /// <summary>
    /// True if the application has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Disposes the application and any related resources.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed)
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

            if (Current == this)
                Current = null!;

            SDL.Quit();
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
    }

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
    public ImmutableList<Display> Displays => Display.Displays;
    #endregion

    #region Event Loop and threading
    /// <summary>
    /// Starts the application event loop running on a new thread.
    /// </summary>
    public static Application Start(SDL.InitFlags flags = DefaultFlags)
    {
        var application = Current;

        if (application == null)
        {
            var tcs = new TaskCompletionSource<Application>();
            var appThread = new Thread(_ =>
            {
                application = new Application(flags);
                application.Run(() => tcs.SetResult(application));
                application.Dispose();
            });
            appThread.Start();
            application = tcs.Task.Result;
        }

        return application;
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
        while (!IsDisposed)
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
                this.OnQuit(e.Quit);
                break;
            case SDL.EventType.Terminating:
                this.OnTerminating(e.Common);
                break;
            case SDL.EventType.LowMemory:
                this.OnLocaleChange(e.Common);
                break;
            case SDL.EventType.WillEnterBackground:
                this.OnEnteringBackground(e.Common);
                break;
            case SDL.EventType.DidEnterBackground:
                this.OnEnteredBackground(e.Common);
                break;
            case SDL.EventType.WillEnterForeground:
                this.OnEnteredForeground(e.Common);
                break;
            case SDL.EventType.DidEnterForeground:
                this.OnEnteringForeground(e.Common);
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
                this.OnMouseDeviceAdded(e.MDevice);
                break;
            case SDL.EventType.MouseRemoved:
                this.OnMouseDeviceRemoved(e.MDevice);
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

    #region Application Events
    public event ApplicationEventHandler<SDL.QuitEvent>? Quitting;
    protected virtual void OnQuit(SDL.QuitEvent e) { this.Quitting?.Invoke(this, e); }

    public event ApplicationEventHandler<SDL.CommonEvent>? Terminating;
    protected virtual void OnTerminating(SDL.CommonEvent e) { this.Terminating?.Invoke(this, e); }

    public event ApplicationEventHandler<SDL.CommonEvent>? LowMemory;
    protected virtual void OnLowMemory(SDL.CommonEvent e) { this.LowMemory?.Invoke(this, e); }

    public event ApplicationEventHandler<SDL.CommonEvent>? EnteringBackground;
    protected virtual void OnEnteringBackground(SDL.CommonEvent e) { this.EnteringBackground?.Invoke(this, e); }

    public event ApplicationEventHandler<SDL.CommonEvent>? EnteredBackground;
    protected virtual void OnEnteredBackground(SDL.CommonEvent e) { this.EnteredBackground?.Invoke(this, e); }

    public event ApplicationEventHandler<SDL.CommonEvent>? EnteringForeground;
    protected virtual void OnEnteringForeground(SDL.CommonEvent e) { this.EnteringForeground?.Invoke(this, e); }

    public event ApplicationEventHandler<SDL.CommonEvent>? EnteredForeground;
    protected virtual void OnEnteredForeground(SDL.CommonEvent e) { this.EnteredForeground?.Invoke(this, e); }

    public event ApplicationEventHandler<SDL.CommonEvent>? LocaleChanged;
    protected virtual void OnLocaleChange(SDL.CommonEvent e) { this.LocaleChanged?.Invoke(this, e); }
    #endregion

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
    protected virtual void OnWindowCloseRequested(Window window, SDL.WindowEvent e) { this.WindowCloseRequested?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowDestroyed;
    protected virtual void OnWindowDestroyed(Window window, SDL.WindowEvent e) { this.WindowDestroyed?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowDisplayChanged;
    protected virtual void OnWindowDisplayChanged(Window window, SDL.WindowEvent e) { this.WindowDisplayChanged?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowDisplayScaleChanged;
    protected virtual void OnWindowDisplayScaleChanged(Window window, SDL.WindowEvent e) { this.WindowDisplayScaleChanged?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowEnterFullscreen;
    protected virtual void OnWindowEnterFullscreen(Window window, SDL.WindowEvent e) { this.WindowEnterFullscreen?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowLeaveFullscreen;
    protected virtual void OnWindowLeaveFullscreen(Window window, SDL.WindowEvent e) { this.WindowLeaveFullscreen?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowFocusGained;
    protected virtual void OnWindowFocusGained(Window window, SDL.WindowEvent e) { this.WindowFocusGained?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowFocusLost;
    protected virtual void OnWindowFocusLost(Window window, SDL.WindowEvent e) { this.WindowFocusLost?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowHidden;
    protected virtual void OnWindowHidden(Window window, SDL.WindowEvent e) { this.WindowHidden?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowShown;
    protected virtual void OnWindowShown(Window window, SDL.WindowEvent e) { this.WindowShown?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowExposed;
    protected virtual void OnWindowExposed(Window window, SDL.WindowEvent e) { this.WindowExposed?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowOccluded;
    protected virtual void OnWindowOccluded(Window window, SDL.WindowEvent e) { this.WindowOccluded?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowMaximized;
    protected virtual void OnWindowMaximized(Window window, SDL.WindowEvent e) { this.WindowMaximized?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowMinimized;
    protected virtual void OnWindowMinimized(Window window, SDL.WindowEvent e) { this.WindowMinimized?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowResized;
    protected virtual void OnWindowResized(Window window, SDL.WindowEvent e) { this.WindowResized?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowRestored;
    protected virtual void OnWindowRestored(Window window, SDL.WindowEvent e) { this.WindowRestored?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowMouseEnter;
    protected virtual void OnWindowMouseEnter(Window window, SDL.WindowEvent e) { this.WindowMouseEnter?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowMouseLeave;
    protected virtual void OnWindowMouseLeave(Window window, SDL.WindowEvent e) { this.WindowMouseLeave?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowMoved;
    protected virtual void OnWindowMoved(Window window, SDL.WindowEvent e) { this.WindowMoved?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowPixelSizeChanged;
    protected virtual void OnWindowPixelSizeChanged(Window window, SDL.WindowEvent e) { this.WindowPixelSizeChanged?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowSafeAreaChanged;
    protected virtual void OnWindowSafeAreaChanged(Window window, SDL.WindowEvent e) { this.WindowSafeAreaChanged?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowHDRStateChanged;
    protected virtual void OnWindowHDRStateChanged(Window window, SDL.WindowEvent e) { this.WindowHDRStateChanged?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowHitTest;
    protected virtual void OnWindowHitTest(Window window, SDL.WindowEvent e) { this.WindowHitTest?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowICCProfChanged;
    protected virtual void OnWindowICCProfChanged(Window window, SDL.WindowEvent e) { this.WindowICCProfChanged?.Invoke(window, e); }

    public event WindowEventHandler<SDL.WindowEvent>? WindowMetalViewResized;
    protected virtual void OnWindowMetalViewResized(Window window, SDL.WindowEvent e) { this.WindowMetalViewResized?.Invoke(window, e); }
    #endregion

    #region Mouse Events

    public event ApplicationEventHandler<SDL.MouseDeviceEvent>? MouseDeviceAdded;
    protected virtual void OnMouseDeviceAdded(SDL.MouseDeviceEvent e) { this.MouseDeviceAdded?.Invoke(this, e); }

    public event ApplicationEventHandler<SDL.MouseDeviceEvent>? MouseDeviceRemoved;
    protected virtual void OnMouseDeviceRemoved(SDL.MouseDeviceEvent e) { this.MouseDeviceRemoved?.Invoke(this, e); }

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

public delegate void ApplicationEventHandler<T>(Application sender, T context);
public delegate void WindowEventHandler<T>(Window sender, T context);

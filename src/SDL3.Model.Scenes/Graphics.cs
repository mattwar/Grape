namespace SDL3.Model.Scenes;

public class Graphics
{
    private Thread? _thread;
    private Window _window;

    private Graphics(Thread thread, Window window)
    {
        _thread = thread;
        _window = window;
    }

    public Window Window => _window;

    public static Graphics Init(int width, int height, 
        SDL.WindowFlags windowFlags = SDL.WindowFlags.Resizable, 
        SDL.InitFlags sdlFlags = SDL.InitFlags.Video)
    {
        var taskSource = new TaskCompletionSource<Graphics>();

        Thread appThread = default!;
        appThread = new Thread(_ =>
        {
            using (var application = new Application(sdlFlags))
            {
                var window = new Window(width, height, SDL.WindowFlags.Resizable);
                var graphics = new Graphics(appThread, window);
                taskSource.SetResult(graphics);
                application.Run();
            }
        });
        appThread.Start();

        return taskSource.Task.Result;
    }

    public void Close()
    {
        var thread = Interlocked.Exchange(ref _thread, null);
        if (thread != null)
        {
            var ev = new SDL.Event { Type = (uint)SDL.EventType.Quit };
            SDL.PushEvent(ref ev);
            thread.Join();
        }
    }

    //public string Title
    //{
    //    get => _window.Title;
    //    set => _window.Title = value;
    //}

    //public SDL.Color BackgroundColor
    //{
    //    get => _window.BackgroundColor;
    //    set => _window.BackgroundColor = value;
    //}

    //public event WindowEventHandler<SDL.KeyboardEvent>? KeyDown
    //{
    //    add => _window.KeyDown += value;
    //    remove => _window.KeyDown -= value;
    //}

    ///// <summary>
    ///// Called to render each frame.
    ///// </summary>
    //public event WindowEventHandler<Renderer>? Rendering
    //{
    //    add => _window.Rendering += value;
    //    remove => _window.Rendering -= value;
    //}
}

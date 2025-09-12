namespace SDL3.Model.Utilities;

/// <summary>
/// Calls the event handler periodically on the initializing thread.
/// </summary>
public class AsyncPeriodicEvent
{
    private readonly AsyncPeriodicTimer _awaiter;

    public Action<TimeSpan, CancellationToken> EventHandler { get; }

    private Task? _task;
    private CancellationTokenSource? _cancellationTokenSource;

    public AsyncPeriodicEvent(TimeSpan period, Action<TimeSpan, CancellationToken> eventHandler)
    {
        _awaiter = new AsyncPeriodicTimer(period);
        this.EventHandler = eventHandler;
    }

    public DateTime StartTime => _awaiter.StartTime;
    public CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? default;

    public TimeSpan Period
    {
        get => _awaiter.Period;
        set => _awaiter.Period = value;
    }

    public void Start()
    {
        if (_task == null)
        {
            _cancellationTokenSource = new CancellationTokenSource();

            // runs update loop on current thread
            _task = RunEventLoopAsync(_cancellationTokenSource.Token);
        }
    }

    private async Task RunEventLoopAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();

        var currentThread = Thread.CurrentThread;

        while (!cancellationToken.IsCancellationRequested)
        {
            var timeSinceStart = DateTime.UtcNow - _awaiter.StartTime;

            try
            {
                this.EventHandler(timeSinceStart, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                    return;

                await _awaiter.NextPeriod(cancellationToken);
            }
            catch
            {
            }
        }
    }

    public async Task StopAsync()
    {
        var runTask = Interlocked.Exchange(ref _task, null);
        if (runTask != null)
        {
            if (_cancellationTokenSource != null)
            {
                await _cancellationTokenSource.CancelAsync();
                _cancellationTokenSource = null;
            }

            await runTask;
        }
    }
}

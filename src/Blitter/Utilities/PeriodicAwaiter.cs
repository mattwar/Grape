using System.Threading.Tasks.Sources;

namespace Blitter.Utilities;

/// <summary>
/// Allocation-free per-frame pacer driven by the <see cref="Application"/>
/// event loop. Use in update / render loops to wait until the next tick.
/// </summary>
/// <remarks>
/// <para>
/// Thread-safe and supports any number of concurrent waiters; every
/// pending waiter is released on each tick.
/// </para>
/// <para>
/// Two flavors are provided:
/// <list type="bullet">
///   <item><see cref="Wait"/> blocks the calling thread until the next
///   tick. Use this from a thread that owns its execution (e.g. a
///   user-driven main thread render loop) so waking does not shift the
///   loop onto another thread.</item>
///   <item><see cref="WaitForNextAsync"/> returns a <see cref="ValueTask"/>
///   whose continuation runs inline on the application thread when the
///   tick fires. <c>await</c>-style consumers without a captured
///   <see cref="SynchronizationContext"/> on their own thread will
///   resume on the application thread.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class PeriodicAwaiter : IDisposable
{
    private readonly object _lock = new();

    // Async path: pooled IValueTaskSource waiters.
    private readonly Stack<AsyncWaiter> _asyncPool = new();
    private readonly List<AsyncWaiter> _asyncPending = new();
    private AsyncWaiter[] _asyncDrainBuffer = new AsyncWaiter[4];

    // Sync path: pooled ManualResetEventSlim wake handles.
    private readonly Stack<ManualResetEventSlim> _syncPool = new();
    private readonly List<ManualResetEventSlim> _syncPending = new();
    private ManualResetEventSlim[] _syncDrainBuffer = new ManualResetEventSlim[4];

    private IDisposable? _tick;
    private bool _disposed;

    public PeriodicAwaiter(TimeSpan period)
    {
        this.Period = period;
        _tick = Application.Current.ScheduleTick(period, OnTick);
    }

    public TimeSpan Period { get; }

    /// <summary>
    /// Blocks the calling thread until the next tick. Allocation-free
    /// in steady state (the wake handle is pooled per call).
    /// </summary>
    public void Wait(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        ManualResetEventSlim mres;
        lock (_lock)
        {
            mres = _syncPool.Count > 0 ? _syncPool.Pop() : new ManualResetEventSlim(false);
            mres.Reset();
            _syncPending.Add(mres);
        }

        try
        {
            mres.Wait(cancellationToken);
        }
        finally
        {
            // Whether we got signalled, were canceled, or threw, return
            // the handle to the pool. If we were canceled before the
            // tick, also remove it from _syncPending so the next tick
            // doesn't try to set a recycled handle.
            lock (_lock)
            {
                if (!mres.IsSet)
                    _syncPending.Remove(mres);
                if (!_disposed)
                    _syncPool.Push(mres);
            }
        }
    }

    /// <summary>
    /// Returns a <see cref="ValueTask"/> that completes at the next tick.
    /// </summary>
    /// <remarks>
    /// The continuation runs on whichever thread the tick fires on
    /// (the application thread). If the calling thread is not the
    /// application thread and has no <see cref="SynchronizationContext"/>
    /// to capture, <c>await</c> will resume on the application thread.
    /// Prefer <see cref="Wait"/> from threads that should not be shifted.
    /// </remarks>
    public ValueTask WaitForNextAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        AsyncWaiter waiter;
        lock (_lock)
        {
            waiter = _asyncPool.Count > 0 ? _asyncPool.Pop() : new AsyncWaiter(this);
            waiter.Reset();
            _asyncPending.Add(waiter);
        }
        return new ValueTask(waiter, waiter.Version);
    }

    private void OnTick()
    {
        // Drain async waiters.
        int asyncCount;
        AsyncWaiter[] asyncBuffer;
        // Drain sync waiters.
        int syncCount;
        ManualResetEventSlim[] syncBuffer;

        lock (_lock)
        {
            asyncCount = _asyncPending.Count;
            if (asyncCount > 0)
            {
                if (_asyncDrainBuffer.Length < asyncCount)
                    _asyncDrainBuffer = new AsyncWaiter[Math.Max(asyncCount, _asyncDrainBuffer.Length * 2)];
                _asyncPending.CopyTo(_asyncDrainBuffer, 0);
                _asyncPending.Clear();
            }
            asyncBuffer = _asyncDrainBuffer;

            syncCount = _syncPending.Count;
            if (syncCount > 0)
            {
                if (_syncDrainBuffer.Length < syncCount)
                    _syncDrainBuffer = new ManualResetEventSlim[Math.Max(syncCount, _syncDrainBuffer.Length * 2)];
                _syncPending.CopyTo(_syncDrainBuffer, 0);
                _syncPending.Clear();
            }
            syncBuffer = _syncDrainBuffer;
        }

        for (var i = 0; i < asyncCount; i++)
        {
            var w = asyncBuffer[i];
            asyncBuffer[i] = null!;
            w.SetResult();
        }

        for (var i = 0; i < syncCount; i++)
        {
            var mres = syncBuffer[i];
            syncBuffer[i] = null!;
            mres.Set();
        }
    }

    // Returns an async waiter to the pool after its result has been observed.
    private void RecycleAsync(AsyncWaiter waiter)
    {
        lock (_lock)
        {
            if (!_disposed)
                _asyncPool.Push(waiter);
        }
    }

    public void Dispose()
    {
        AsyncWaiter[]? asyncToRelease = null;
        int asyncReleaseCount = 0;
        ManualResetEventSlim[]? syncToRelease = null;
        int syncReleaseCount = 0;

        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;

            asyncReleaseCount = _asyncPending.Count;
            if (asyncReleaseCount > 0)
            {
                if (_asyncDrainBuffer.Length < asyncReleaseCount)
                    _asyncDrainBuffer = new AsyncWaiter[asyncReleaseCount];
                _asyncPending.CopyTo(_asyncDrainBuffer, 0);
                _asyncPending.Clear();
                asyncToRelease = _asyncDrainBuffer;
            }
            _asyncPool.Clear();

            syncReleaseCount = _syncPending.Count;
            if (syncReleaseCount > 0)
            {
                if (_syncDrainBuffer.Length < syncReleaseCount)
                    _syncDrainBuffer = new ManualResetEventSlim[syncReleaseCount];
                _syncPending.CopyTo(_syncDrainBuffer, 0);
                _syncPending.Clear();
                syncToRelease = _syncDrainBuffer;
            }
            // Don't dispose pooled handles immediately; in-flight Wait
            // callers are still using one. We just stop pooling further.
            _syncPool.Clear();
        }

        _tick?.Dispose();
        _tick = null;

        // Wake outstanding waiters so they don't hang. They should next
        // observe the disposed/closed state on their own terms.
        if (asyncToRelease is not null)
        {
            for (var i = 0; i < asyncReleaseCount; i++)
            {
                var w = asyncToRelease[i];
                asyncToRelease[i] = null!;
                w.SetResult();
            }
        }
        if (syncToRelease is not null)
        {
            for (var i = 0; i < syncReleaseCount; i++)
            {
                var mres = syncToRelease[i];
                syncToRelease[i] = null!;
                mres.Set();
            }
        }
    }

    private sealed class AsyncWaiter : IValueTaskSource
    {
        private readonly PeriodicAwaiter _owner;
        private ManualResetValueTaskSourceCore<bool> _core;

        public AsyncWaiter(PeriodicAwaiter owner)
        {
            _owner = owner;
            // Inline: SetResult is called from the app thread (where
            // ScheduleTick fires); the await machinery will then either
            // post the continuation through the caller's captured
            // SynchronizationContext (if any) or run it inline there.
            _core.RunContinuationsAsynchronously = false;
        }

        public short Version => _core.Version;
        public void Reset() => _core.Reset();
        public void SetResult() => _core.SetResult(true);

        void IValueTaskSource.GetResult(short token)
        {
            try { _core.GetResult(token); }
            finally { _owner.RecycleAsync(this); }
        }

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => _core.GetStatus(token);

        void IValueTaskSource.OnCompleted(
            Action<object?> continuation, object? state, short token,
            ValueTaskSourceOnCompletedFlags flags)
            => _core.OnCompleted(continuation, state, token, flags);
    }
}

using System.Diagnostics;

namespace Grape.Utilities;

/// <summary>
/// Awaits until the start of the next period, returning on the same thread.
/// </summary>
public class AsyncPeriodicTimer
{
    private long _startTs = Stopwatch.GetTimestamp();

    /// <summary>
    /// The period between ticks.
    /// </summary>
    public TimeSpan Period { get; set; }

    /// <summary>
    /// The wall-clock time at which the period schedule started. Reset
    /// via <see cref="Reset"/> to align future ticks to "now".
    /// </summary>
    public DateTime StartTime { get; private set; } = DateTime.UtcNow;

    public AsyncPeriodicTimer(TimeSpan period)
    {
        this.Period = period;
    }

    /// <summary>
    /// Realigns the period schedule to begin "now". Future
    /// <see cref="NextPeriod"/> awaits will be paced from this point.
    /// </summary>
    public void Reset()
    {
        _startTs = Stopwatch.GetTimestamp();
        this.StartTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns a task that completes at the start of the next period.
    /// Successive calls align to absolute period boundaries so cadence
    /// stays steady across calls.
    /// </summary>
    public Task NextPeriod(CancellationToken cancellationToken = default)
    {
        var period = this.Period;
        if (period <= TimeSpan.Zero)
            return Task.CompletedTask;

        var elapsed = Stopwatch.GetElapsedTime(_startTs);
        var nthPeriod = (long)(elapsed.Ticks / period.Ticks);
        var nextStart = TimeSpan.FromTicks((nthPeriod + 1) * period.Ticks);
        var delay = nextStart - elapsed;
        return delay > TimeSpan.Zero
            ? Task.Delay(delay, cancellationToken)
            : Task.CompletedTask;
    }
}

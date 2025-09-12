namespace SDL3.Model.Utilities;

/// <summary>
/// Awaits until the start of the next period, returning on the same thread.
/// </summary>
public class AsyncPeriodicTimer
{
    public DateTime StartTime { get; set; }
    public TimeSpan Period { get; set; }

    public AsyncPeriodicTimer(TimeSpan period)
    {
        this.Period = period;
        this.StartTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns a task that completes at the start of the next period.
    /// </summary>
    public Task NextPeriod(CancellationToken cancellationToken = default)
    {
        var timeSinceStart = DateTime.UtcNow - this.StartTime;
        var period = this.Period;
        var nthPeriod = (long)(timeSinceStart.TotalMilliseconds / this.Period.TotalMilliseconds);
        var thisPeriodStart = TimeSpan.FromMilliseconds(nthPeriod * this.Period.TotalMilliseconds);
        var nextPeriodStart = TimeSpan.FromMilliseconds((nthPeriod + 1) * this.Period.TotalMilliseconds);
        var delay = nextPeriodStart - timeSinceStart;
        return Task.Delay(delay, cancellationToken);
    }
}

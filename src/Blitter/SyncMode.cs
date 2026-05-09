namespace Blitter;

/// <summary>
/// How a renderer schedules its frame presentation against the display's
/// refresh cycle. Values are treated as a hint: if the requested mode
/// isn't supported by the underlying backend, the renderer falls back to
/// the next-best supported mode rather than throwing.
/// </summary>
public enum SyncMode
{
    /// <summary>
    /// Wait for the next vertical blank before presenting. No tearing,
    /// steady cadence at (a divisor of) the display's refresh rate.
    /// Always supported. The default.
    /// </summary>
    WaitForSync = 0,

    /// <summary>
    /// Present as soon as a frame is ready. May tear, but minimises
    /// latency. Falls back to <see cref="WaitForSync"/> if the backend
    /// doesn't support immediate presentation.
    /// </summary>
    Immediate,

    /// <summary>
    /// Replace any queued-but-not-yet-presented frame with the newest
    /// one at the next vertical blank. No tearing, low latency. Falls
    /// back to <see cref="Immediate"/> if unsupported, then to
    /// <see cref="WaitForSync"/>.
    /// </summary>
    Latest,
}

namespace Blitter;

/// <summary>
/// Contract for a per-frame inputs struct passed to a stateful object's
/// <c>Update</c> method. The base contract carries only timings;
/// dimensional variants (<see cref="IUpdateContext2D"/>,
/// <see cref="IUpdateContext3D"/>) add domain-specific information.
/// </summary>
/// <remarks>
/// Implementations should be value types (<c>readonly struct</c>) so
/// callers can pass them by-<c>in</c> reference for zero-allocation
/// updates. The interface exists so generic update methods can be
/// constrained to <c>where TCtx : IUpdateContext</c> and still receive
/// the typed extras of the concrete struct.
/// </remarks>
public interface IUpdateContext
{
    /// <summary>Wall-clock time since the host's clock started.</summary>
    TimeSpan ElapsedSinceStart { get; }

    /// <summary>
    /// Wall-clock time since the previous update, clamped by the host's
    /// frame-delta cap so a long pause doesn't teleport time-integrated
    /// state.
    /// </summary>
    TimeSpan ElapsedSinceLastUpdate { get; }
}

/// <summary>
/// 2D-domain update context. Adds a pixel <see cref="Bounds"/> rectangle
/// for layout, hit-testing, or constraining movement to the visible area.
/// </summary>
public interface IUpdateContext2D : IUpdateContext
{
    /// <summary>Logical update region in target pixels.</summary>
    Rect Bounds { get; }
}

/// <summary>
/// 3D-domain update context. Carries only the base timings today; reserved
/// as the extension point for future world-space information (frustum,
/// world AABB, etc.) that 3D simulations may eventually need.
/// </summary>
public interface IUpdateContext3D : IUpdateContext
{
}

/// <summary>
/// Bare update context: timings only. Suitable for any stateful object
/// whose <c>Update</c> needs nothing beyond the clock.
/// </summary>
public readonly struct UpdateContext : IUpdateContext
{
    public TimeSpan ElapsedSinceStart { get; init; }
    public TimeSpan ElapsedSinceLastUpdate { get; init; }
}

/// <summary>
/// 2D update context: timings + pixel <see cref="Bounds"/>. Suitable for
/// 2D simulations / UI that need to know the screen rectangle.
/// </summary>
public readonly struct UpdateContext2D : IUpdateContext2D
{
    public TimeSpan ElapsedSinceStart { get; init; }
    public TimeSpan ElapsedSinceLastUpdate { get; init; }
    public Rect Bounds { get; init; }
}

/// <summary>
/// 3D update context: timings only today. A distinct type so consumers
/// can opt into 3D semantics now and pick up additional 3D-specific
/// fields later without a churning API change.
/// </summary>
public readonly struct UpdateContext3D : IUpdateContext3D
{
    public TimeSpan ElapsedSinceStart { get; init; }
    public TimeSpan ElapsedSinceLastUpdate { get; init; }
}

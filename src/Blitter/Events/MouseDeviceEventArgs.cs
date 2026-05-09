namespace Blitter.Events;

/// <summary>
/// A mouse device was added or removed at the application level.
/// </summary>
/// <param name="MouseId">The instance id of the mouse that was added or removed.</param>
public readonly record struct MouseDeviceEventArgs(uint MouseId);

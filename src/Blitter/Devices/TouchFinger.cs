using System.Numerics;

namespace Blitter.Devices;

/// <summary>
/// A snapshot of one finger currently in contact with a touch device.
/// Coordinates are normalized to [0, 1] relative to the device or window
/// (depending on <see cref="TouchDevice.Type"/>).
/// </summary>
public readonly struct TouchFinger
{
    public TouchFinger(FingerId id, Vector2 position, float pressure)
    {
        Id = id;
        Position = position;
        Pressure = pressure;
    }

    /// <summary>
    /// Stable id of this finger contact, valid for the lifetime of the touch.
    /// </summary>
    public FingerId Id { get; }

    /// <summary>
    /// Position normalized to [0, 1].
    /// </summary>
    public Vector2 Position { get; }

    /// <summary>
    /// Pressure normalized to [0, 1]. Zero on devices that do not report
    /// pressure.
    /// </summary>
    public float Pressure { get; }
}

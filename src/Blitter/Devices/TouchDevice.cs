using System.Numerics;

namespace Blitter.Devices;

/// <summary>
/// A registered touch device. Touch devices do not need to be opened or
/// closed; they are queried on demand.
/// </summary>
public sealed class TouchDevice
{
    private readonly TouchId _id;

    internal TouchDevice(TouchId id)
    {
        _id = id;
    }

    /// <summary>
    /// The stable id of this touch device.
    /// </summary>
    public TouchId Id => _id;

    /// <summary>
    /// The implementation-dependent name of the device, or null if not
    /// reported by the driver.
    /// </summary>
    public string? Name => SDL.GetTouchDeviceName(_id.Value);

    /// <summary>
    /// The kind of device (touchscreen vs trackpad).
    /// </summary>
    public TouchDeviceType Type
        => (TouchDeviceType)SDL.GetTouchDeviceType(_id.Value);

    /// <summary>
    /// Returns a snapshot of all fingers currently in contact with this
    /// device. Coordinates are normalized to [0, 1].
    /// </summary>
    public TouchFinger[] GetFingers()
    {
        var raw = SDL.GetTouchFingers(_id.Value, out var count);
        if (raw == null || count <= 0)
            return System.Array.Empty<TouchFinger>();

        var result = new TouchFinger[count];
        for (var i = 0; i < count; i++)
        {
            var f = raw[i];
            result[i] = new TouchFinger(
                new FingerId(f.ID),
                new Vector2(f.X, f.Y),
                f.Pressure);
        }
        return result;
    }
}

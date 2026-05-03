using System.Numerics;

namespace Grape;

/// <summary>
/// The kind of touch device.
/// </summary>
public enum TouchDeviceType
{
    /// <summary>Could not determine the device type.</summary>
    Unknown = -1,
    /// <summary>A touchscreen reporting window-relative coordinates.</summary>
    Direct = 0,
    /// <summary>A trackpad with absolute device coordinates.</summary>
    IndirectAbsolute,
    /// <summary>A trackpad with cursor-relative coordinates.</summary>
    IndirectRelative,
}

/// <summary>
/// Stable identifier for a touch device.
/// </summary>
public readonly record struct TouchId(ulong Value)
{
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Stable identifier for an individual finger on a touch device. The same id
/// is used across down/motion/up reports for one continuous touch.
/// </summary>
public readonly record struct FingerId(ulong Value)
{
    public override string ToString() => Value.ToString();
}

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

/// <summary>
/// Polling access to registered touch devices.
/// </summary>
public static class Touch
{
    /// <summary>
    /// True if at least one touch device is currently registered. Note that
    /// on some platforms a device is not registered until it is first used.
    /// </summary>
    public static bool HasTouch
    {
        get
        {
            _ = Application.Current;
            var ids = SDL.GetTouchDevices(out var count);
            return ids != null && count > 0;
        }
    }

    /// <summary>
    /// Snapshot of the currently-registered touch devices.
    /// </summary>
    public static IReadOnlyList<TouchDevice> Devices
    {
        get
        {
            _ = Application.Current;
            var ids = SDL.GetTouchDevices(out var count);
            if (ids == null || count == 0)
                return System.Array.Empty<TouchDevice>();

            var devices = new TouchDevice[count];
            for (var i = 0; i < count; i++)
                devices[i] = new TouchDevice(new TouchId(ids[i]));
            return devices;
        }
    }

    /// <summary>
    /// The first registered touch device, or null if none are available.
    /// </summary>
    public static TouchDevice? Primary
    {
        get
        {
            var list = Devices;
            return list.Count == 0 ? null : list[0];
        }
    }

    /// <summary>
    /// Returns the touch device with the given id, or null if no such
    /// device is registered.
    /// </summary>
    public static TouchDevice? ById(TouchId id)
    {
        foreach (var d in Devices)
        {
            if (d.Id == id)
                return d;
        }
        return null;
    }
}

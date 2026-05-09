namespace Blitter.Devices;

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

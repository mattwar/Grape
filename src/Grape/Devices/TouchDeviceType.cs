namespace Grape.Devices;

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

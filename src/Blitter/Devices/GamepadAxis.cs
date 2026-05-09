namespace Blitter.Devices;

/// <summary>
/// A standard gamepad axis. Stick axes report values in [-1, 1]; trigger axes
/// report values in [0, 1].
/// </summary>
public enum GamepadAxis : byte
{
    LeftX = 0,
    LeftY,
    RightX,
    RightY,
    LeftTrigger,
    RightTrigger,
}

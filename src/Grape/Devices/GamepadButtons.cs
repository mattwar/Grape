namespace Grape.Devices;

/// <summary>
/// A bitmask of gamepad buttons currently held down. Bit position matches
/// the corresponding <see cref="GamepadButton"/> value.
/// </summary>
[Flags]
public enum GamepadButtons : uint
{
    None = 0,
    South        = 1u << 0,
    East         = 1u << 1,
    West         = 1u << 2,
    North        = 1u << 3,
    Back         = 1u << 4,
    Guide        = 1u << 5,
    Start        = 1u << 6,
    LeftStick    = 1u << 7,
    RightStick   = 1u << 8,
    LeftShoulder = 1u << 9,
    RightShoulder = 1u << 10,
    DPadUp       = 1u << 11,
    DPadDown     = 1u << 12,
    DPadLeft     = 1u << 13,
    DPadRight    = 1u << 14,
    Misc1        = 1u << 15,
    RightPaddle1 = 1u << 16,
    LeftPaddle1  = 1u << 17,
    RightPaddle2 = 1u << 18,
    LeftPaddle2  = 1u << 19,
    Touchpad     = 1u << 20,
    Misc2        = 1u << 21,
    Misc3        = 1u << 22,
    Misc4        = 1u << 23,
    Misc5        = 1u << 24,
    Misc6        = 1u << 25,
}

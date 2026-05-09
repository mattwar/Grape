namespace Blitter.Devices;

/// <summary>
/// The recognized standard gamepad type. Third-party controllers may report as
/// the layout they most closely match.
/// </summary>
public enum GamepadType
{
    Unknown = 0,
    Standard,
    Xbox360,
    XboxOne,
    PS3,
    PS4,
    PS5,
    NintendoSwitchPro,
    NintendoSwitchJoyConLeft,
    NintendoSwitchJoyConRight,
    NintendoSwitchJoyConPair,
    GameCube,
}

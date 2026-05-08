namespace Grape.Devices;

/// <summary>
/// A standard gamepad button. Values follow SDL's "south/east/west/north"
/// face-button convention (e.g. on Xbox: South=A, East=B, West=X, North=Y).
/// </summary>
public enum GamepadButton : byte
{
    South = 0,
    East,
    West,
    North,
    Back,
    Guide,
    Start,
    LeftStick,
    RightStick,
    LeftShoulder,
    RightShoulder,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    /// <summary>Additional button (e.g. Xbox Series X share, PS5 mic, Switch capture).</summary>
    Misc1,
    /// <summary>Upper / primary right-hand paddle (e.g. Xbox Elite P1).</summary>
    RightPaddle1,
    /// <summary>Upper / primary left-hand paddle (e.g. Xbox Elite P3).</summary>
    LeftPaddle1,
    /// <summary>Lower / secondary right-hand paddle (e.g. Xbox Elite P2).</summary>
    RightPaddle2,
    /// <summary>Lower / secondary left-hand paddle (e.g. Xbox Elite P4).</summary>
    LeftPaddle2,
    /// <summary>PS4 / PS5 touchpad button.</summary>
    Touchpad,
    Misc2,
    Misc3,
    Misc4,
    Misc5,
    Misc6,
}

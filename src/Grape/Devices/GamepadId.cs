namespace Grape.Devices;

/// <summary>
/// Stable identifier for a connected gamepad. The same physical device retains
/// the same id for as long as it remains connected.
/// </summary>
public readonly record struct GamepadId(uint Value)
{
    public override string ToString() => Value.ToString();
}

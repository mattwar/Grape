namespace Grape.Devices;

/// <summary>
/// Stable identifier for an individual finger on a touch device. The same id
/// is used across down/motion/up reports for one continuous touch.
/// </summary>
public readonly record struct FingerId(ulong Value)
{
    public override string ToString() => Value.ToString();
}

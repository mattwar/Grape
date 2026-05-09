namespace Blitter.Devices;

/// <summary>
/// Stable identifier for a touch device.
/// </summary>
public readonly record struct TouchId(ulong Value)
{
    public override string ToString() => Value.ToString();
}

namespace Blitter;

/// <summary>Mouse button state flags.</summary>
[Flags]
public enum MouseButtons : uint
{
    None = 0,
    Left = 1u << 0,
    Middle = 1u << 1,
    Right = 1u << 2,
    X1 = 1u << 3,
    X2 = 1u << 4,
}

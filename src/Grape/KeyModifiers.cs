namespace Grape;

/// <summary>Keyboard modifier flags.</summary>
[Flags]
public enum KeyModifiers : ushort
{
    None     = 0x0000,
    LShift   = 0x0001,
    RShift   = 0x0002,
    Level5   = 0x0004,
    LCtrl    = 0x0040,
    RCtrl    = 0x0080,
    LAlt     = 0x0100,
    RAlt     = 0x0200,
    LGui     = 0x0400,
    RGui     = 0x0800,
    Num      = 0x1000,
    Caps     = 0x2000,
    Mode     = 0x4000,
    Scroll   = 0x8000,
    Ctrl     = LCtrl | RCtrl,
    Shift    = LShift | RShift,
    Alt      = LAlt | RAlt,
    Gui      = LGui | RGui,
}

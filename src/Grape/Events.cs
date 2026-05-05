using System.Numerics;
using System.Runtime.InteropServices;

namespace Grape;

/// <summary>Mouse buttons.</summary>
public enum MouseButton : byte
{
    Left = 1,
    Middle = 2,
    Right = 3,
    X1 = 4,
    X2 = 5,
}

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

/// <summary>Mouse wheel scroll direction.</summary>
public enum MouseWheelDirection
{
    Normal = 0,
    Flipped = 1,
}

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

/// <summary>
/// Virtual key codes. Values mirror the underlying SDL3 keycode values exactly,
/// so unknown values can be safely cast from the underlying uint.
/// </summary>
public enum Key : uint
{
    Unknown = 0x00000000u,
    Backspace = 0x00000008u,
    Tab = 0x00000009u,
    Return = 0x0000000du,
    Escape = 0x0000001bu,
    Space = 0x00000020u,
    Exclaim = 0x00000021u,
    Hash = 0x00000023u,
    Dollar = 0x00000024u,
    Percent = 0x00000025u,
    Ampersand = 0x00000026u,
    Apostrophe = 0x00000027u,
    LeftParen = 0x00000028u,
    RightParen = 0x00000029u,
    Asterisk = 0x0000002au,
    Plus = 0x0000002bu,
    Comma = 0x0000002cu,
    Minus = 0x0000002du,
    Period = 0x0000002eu,
    Slash = 0x0000002fu,
    D0 = 0x00000030u,
    D1 = 0x00000031u,
    D2 = 0x00000032u,
    D3 = 0x00000033u,
    D4 = 0x00000034u,
    D5 = 0x00000035u,
    D6 = 0x00000036u,
    D7 = 0x00000037u,
    D8 = 0x00000038u,
    D9 = 0x00000039u,
    Colon = 0x0000003au,
    Semicolon = 0x0000003bu,
    Less = 0x0000003cu,
    Equals = 0x0000003du,
    Greater = 0x0000003eu,
    Question = 0x0000003fu,
    At = 0x00000040u,
    LeftBracket = 0x0000005bu,
    Backslash = 0x0000005cu,
    RightBracket = 0x0000005du,
    Caret = 0x0000005eu,
    Underscore = 0x0000005fu,
    Grave = 0x00000060u,
    A = 0x00000061u,
    B = 0x00000062u,
    C = 0x00000063u,
    D = 0x00000064u,
    E = 0x00000065u,
    F = 0x00000066u,
    G = 0x00000067u,
    H = 0x00000068u,
    I = 0x00000069u,
    J = 0x0000006au,
    K = 0x0000006bu,
    L = 0x0000006cu,
    M = 0x0000006du,
    N = 0x0000006eu,
    O = 0x0000006fu,
    P = 0x00000070u,
    Q = 0x00000071u,
    R = 0x00000072u,
    S = 0x00000073u,
    T = 0x00000074u,
    U = 0x00000075u,
    V = 0x00000076u,
    W = 0x00000077u,
    X = 0x00000078u,
    Y = 0x00000079u,
    Z = 0x0000007au,
    Delete = 0x0000007fu,
    CapsLock = 0x40000039u,
    F1 = 0x4000003au,
    F2 = 0x4000003bu,
    F3 = 0x4000003cu,
    F4 = 0x4000003du,
    F5 = 0x4000003eu,
    F6 = 0x4000003fu,
    F7 = 0x40000040u,
    F8 = 0x40000041u,
    F9 = 0x40000042u,
    F10 = 0x40000043u,
    F11 = 0x40000044u,
    F12 = 0x40000045u,
    PrintScreen = 0x40000046u,
    ScrollLock = 0x40000047u,
    Pause = 0x40000048u,
    Insert = 0x40000049u,
    Home = 0x4000004au,
    PageUp = 0x4000004bu,
    End = 0x4000004du,
    PageDown = 0x4000004eu,
    Right = 0x4000004fu,
    Left = 0x40000050u,
    Down = 0x40000051u,
    Up = 0x40000052u,
    NumLockClear = 0x40000053u,
    KpDivide = 0x40000054u,
    KpMultiply = 0x40000055u,
    KpMinus = 0x40000056u,
    KpPlus = 0x40000057u,
    KpEnter = 0x40000058u,
    Kp0 = 0x40000062u,
    Kp1 = 0x40000059u,
    Kp2 = 0x4000005au,
    Kp3 = 0x4000005bu,
    Kp4 = 0x4000005cu,
    Kp5 = 0x4000005du,
    Kp6 = 0x4000005eu,
    Kp7 = 0x4000005fu,
    Kp8 = 0x40000060u,
    Kp9 = 0x40000061u,
    KpPeriod = 0x40000063u,
    Application = 0x40000065u,
    LCtrl = 0x400000e0u,
    LShift = 0x400000e1u,
    LAlt = 0x400000e2u,
    LGui = 0x400000e3u,
    RCtrl = 0x400000e4u,
    RShift = 0x400000e5u,
    RAlt = 0x400000e6u,
    RGui = 0x400000e7u,
}

public readonly record struct KeyEventArgs(
    Key Key,
    KeyModifiers Modifiers,
    bool IsDown,
    bool IsRepeat);

public readonly record struct MouseMoveEventArgs(
    Vector2 Position,
    Vector2 Delta,
    MouseButtons Buttons);

public readonly record struct MouseButtonEventArgs(
    MouseButton Button,
    bool IsDown,
    int Clicks,
    Vector2 Position);

public readonly record struct MouseWheelEventArgs(
    Vector2 Scroll,
    Vector2 MousePosition,
    MouseWheelDirection Direction);

public readonly record struct TextInputEventArgs(string Text);

public readonly record struct TextEditingEventArgs(string Text, int Start, int Length);

// Window event args -- one per window event variant. Most are payload-less
// markers so signatures are stable if data is added later.
public readonly record struct WindowCloseRequestedEventArgs;
public readonly record struct WindowDestroyedEventArgs;
public readonly record struct WindowDisplayChangedEventArgs(Display? Display);
public readonly record struct WindowDisplayScaleChangedEventArgs;
public readonly record struct WindowEnterFullscreenEventArgs;
public readonly record struct WindowLeaveFullscreenEventArgs;
public readonly record struct WindowFocusGainedEventArgs;
public readonly record struct WindowFocusLostEventArgs;
public readonly record struct WindowHiddenEventArgs;
public readonly record struct WindowShownEventArgs;
public readonly record struct WindowExposedEventArgs;
public readonly record struct WindowOccludedEventArgs;
public readonly record struct WindowMaximizedEventArgs;
public readonly record struct WindowMinimizedEventArgs;
public readonly record struct WindowResizedEventArgs(int Width, int Height);
public readonly record struct WindowRestoredEventArgs;
public readonly record struct WindowMouseEnterEventArgs;
public readonly record struct WindowMouseLeaveEventArgs;
public readonly record struct WindowMovedEventArgs(int X, int Y);
public readonly record struct WindowPixelSizeChangedEventArgs(int Width, int Height);
public readonly record struct WindowSafeAreaChangedEventArgs;
public readonly record struct WindowHDRStateChangedEventArgs;
public readonly record struct WindowHitTestEventArgs;
public readonly record struct WindowICCProfChangedEventArgs;
public readonly record struct WindowMetalViewResizedEventArgs(int Width, int Height);

internal static class EventArgsFactory
{
    public static KeyEventArgs Key(SDL.KeyboardEvent e)
        => new((Key)e.Key, (KeyModifiers)e.Mod, e.Down, e.Repeat);

    public static MouseMoveEventArgs MouseMove(SDL.MouseMotionEvent e)
        => new(new Vector2(e.X, e.Y), new Vector2(e.XRel, e.YRel), (MouseButtons)e.State);

    public static MouseButtonEventArgs MouseButton(SDL.MouseButtonEvent e)
        => new((MouseButton)e.Button, e.Down, e.Clicks, new Vector2(e.X, e.Y));

    public static MouseWheelEventArgs MouseWheel(SDL.MouseWheelEvent e)
        => new(new Vector2(e.X, e.Y), new Vector2(e.MouseX, e.MouseY), (MouseWheelDirection)e.Direction);

    public static TextInputEventArgs TextInput(SDL.TextInputEvent e)
        => new(Marshal.PtrToStringUTF8(e.Text) ?? "");

    public static TextEditingEventArgs TextEditing(SDL.TextEditingEvent e)
        => new(Marshal.PtrToStringUTF8(e.Text) ?? "", e.Start, e.Length);
}

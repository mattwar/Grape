namespace Grape;

/// <summary>
/// Identifies a key by its physical position on the keyboard, independent of
/// the active keyboard layout. Values are based on the USB HID usage table and
/// match the underlying SDL3 scancode values exactly, so unknown values can be
/// safely cast from the underlying int.
/// </summary>
public enum PhysicalKey
{
    Unknown = 0,

    A = 4, B = 5, C = 6, D = 7, E = 8, F = 9, G = 10, H = 11, I = 12, J = 13,
    K = 14, L = 15, M = 16, N = 17, O = 18, P = 19, Q = 20, R = 21, S = 22, T = 23,
    U = 24, V = 25, W = 26, X = 27, Y = 28, Z = 29,

    D1 = 30, D2 = 31, D3 = 32, D4 = 33, D5 = 34,
    D6 = 35, D7 = 36, D8 = 37, D9 = 38, D0 = 39,

    Return = 40,
    Escape = 41,
    Backspace = 42,
    Tab = 43,
    Space = 44,
    Minus = 45,
    Equals = 46,
    LeftBracket = 47,
    RightBracket = 48,
    Backslash = 49,
    Semicolon = 51,
    Apostrophe = 52,
    Grave = 53,
    Comma = 54,
    Period = 55,
    Slash = 56,
    CapsLock = 57,

    F1 = 58, F2 = 59, F3 = 60, F4 = 61, F5 = 62, F6 = 63,
    F7 = 64, F8 = 65, F9 = 66, F10 = 67, F11 = 68, F12 = 69,

    PrintScreen = 70,
    ScrollLock = 71,
    Pause = 72,
    Insert = 73,
    Home = 74,
    PageUp = 75,
    Delete = 76,
    End = 77,
    PageDown = 78,
    Right = 79,
    Left = 80,
    Down = 81,
    Up = 82,
    NumLockClear = 83,

    KpDivide = 84,
    KpMultiply = 85,
    KpMinus = 86,
    KpPlus = 87,
    KpEnter = 88,
    Kp1 = 89, Kp2 = 90, Kp3 = 91, Kp4 = 92, Kp5 = 93,
    Kp6 = 94, Kp7 = 95, Kp8 = 96, Kp9 = 97, Kp0 = 98,
    KpPeriod = 99,

    Application = 101,
    Power = 102,
    KpEquals = 103,

    F13 = 104, F14 = 105, F15 = 106, F16 = 107, F17 = 108, F18 = 109,
    F19 = 110, F20 = 111, F21 = 112, F22 = 113, F23 = 114, F24 = 115,

    Menu = 118,
    Mute = 127,
    VolumeUp = 128,
    VolumeDown = 129,

    LCtrl = 224,
    LShift = 225,
    LAlt = 226,
    LGui = 227,
    RCtrl = 228,
    RShift = 229,
    RAlt = 230,
    RGui = 231,
}

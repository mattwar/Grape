namespace Grape;

/// <summary>
/// Provides access to global keyboard state, independent of
/// <see cref="Window.KeyDown"/> / <see cref="Window.KeyUp"/> events.
/// </summary>
public static class Keyboard
{
    /// <summary>
    /// True if at least one keyboard (or keyboard-like input device) is currently connected.
    /// </summary>
    public static bool HasKeyboard
    {
        get
        {
            _ = Application.Current;
            return SDL.HasKeyboard();
        }
    }

    /// <summary>
    /// The current key modifier state (Shift, Ctrl, Alt, etc.).
    /// </summary>
    public static KeyModifiers Modifiers
    {
        get
        {
            _ = Application.Current;
            return (KeyModifiers)SDL.GetModState();
        }

        set
        {
            _ = Application.Current;
            SDL.SetModState((SDL.Keymod)value);
        }
    }

    /// <summary>
    /// Returns true if the physical key at the given keyboard position is
    /// currently pressed. Physical-key checks are independent of the active
    /// keyboard layout — useful for game-style input where you want the same
    /// physical position (e.g. WASD) to work across QWERTY/AZERTY/Dvorak/etc.
    /// </summary>
    public static bool IsDown(PhysicalKey physicalKey)
    {
        _ = Application.Current;
        var state = SDL.GetKeyboardState(out var numKeys);
        var idx = (int)physicalKey;
        return (uint)idx < (uint)numKeys && state[idx];
    }

    /// <summary>
    /// Returns true if the (layout-dependent) virtual <see cref="Key"/> is
    /// currently pressed. Use this for shortcut-style checks (e.g. is Escape held)
    /// where the produced character matters more than the physical position.
    /// </summary>
    public static bool IsDown(Key key)
    {
        var sc = SDL.GetScancodeFromKey((SDL.Keycode)key, out _);
        return IsDown((PhysicalKey)sc);
    }

    /// <summary>
    /// Gets a human-readable name for a virtual key.
    /// Returns an empty string if the key has no name.
    /// </summary>
    public static string GetName(Key key)
    {
        _ = Application.Current;
        return SDL.GetKeyName((SDL.Keycode)key);
    }

    /// <summary>
    /// Gets a human-readable name for a physical key.
    /// Note: names are not stable across platforms (e.g. "Left GUI" on Linux vs. "Left Windows" on Windows).
    /// </summary>
    public static string GetName(PhysicalKey physicalKey)
    {
        _ = Application.Current;
        return SDL.GetScancodeName((SDL.Scancode)physicalKey);
    }

    /// <summary>
    /// Returns the <see cref="PhysicalKey"/> that currently produces the given
    /// virtual <see cref="Key"/> under the active keyboard layout.
    /// </summary>
    public static PhysicalKey GetPhysicalKey(Key key)
    {
        _ = Application.Current;
        return (PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)key, out _);
    }

    /// <summary>
    /// Returns the virtual <see cref="Key"/> that the given <see cref="PhysicalKey"/>
    /// produces under the active keyboard layout, optionally taking modifiers into account.
    /// </summary>
    public static Key GetKey(PhysicalKey physicalKey, KeyModifiers modifiers = KeyModifiers.None)
    {
        _ = Application.Current;
        return (Key)SDL.GetKeyFromScancode((SDL.Scancode)physicalKey, (SDL.Keymod)modifiers, false);
    }

    /// <summary>
    /// Enumerates every <see cref="PhysicalKey"/> currently pressed without
    /// allocating. Use with <c>foreach</c>:
    /// <code>foreach (var pk in Keyboard.PressedPhysical) { ... }</code>
    /// </summary>
    public static PressedPhysicalKeys PressedPhysical
    {
        get
        {
            _ = Application.Current;
            return new PressedPhysicalKeys(SDL.GetKeyboardState(out _));
        }
    }

    /// <summary>
    /// Enumerates every virtual <see cref="Key"/> currently pressed (resolved
    /// against the active keyboard layout and current modifier state) without
    /// allocating. Use with <c>foreach</c>.
    /// </summary>
    public static PressedKeys Pressed
    {
        get
        {
            _ = Application.Current;
            return new PressedKeys(SDL.GetKeyboardState(out _), SDL.GetModState());
        }
    }

    /// <summary>
    /// Releases all currently pressed keys, generating key-up events for each.
    /// </summary>
    public static void Reset()
    {
        _ = Application.Current;
        SDL.ResetKeyboard();
    }
}

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

/// <summary>
/// Zero-allocation <c>foreach</c> source for currently pressed physical keys.
/// </summary>
public readonly ref struct PressedPhysicalKeys
{
    private readonly ReadOnlySpan<bool> _state;

    internal PressedPhysicalKeys(ReadOnlySpan<bool> state)
    {
        _state = state;
    }

    public Enumerator GetEnumerator() => new(_state);

    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<bool> _state;
        private int _index;

        internal Enumerator(ReadOnlySpan<bool> state)
        {
            _state = state;
            _index = -1;
        }

        public PhysicalKey Current => (PhysicalKey)_index;

        public bool MoveNext()
        {
            while (++_index < _state.Length)
            {
                if (_state[_index])
                    return true;
            }
            return false;
        }
    }
}

/// <summary>
/// Zero-allocation <c>foreach</c> source for currently pressed virtual keys.
/// Each yielded <see cref="Key"/> is resolved against the active keyboard
/// layout and the modifier state captured when iteration began.
/// </summary>
public readonly ref struct PressedKeys
{
    private readonly ReadOnlySpan<bool> _state;
    private readonly SDL.Keymod _modifiers;

    internal PressedKeys(ReadOnlySpan<bool> state, SDL.Keymod modifiers)
    {
        _state = state;
        _modifiers = modifiers;
    }

    public Enumerator GetEnumerator() => new(_state, _modifiers);

    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<bool> _state;
        private readonly SDL.Keymod _modifiers;
        private int _index;

        internal Enumerator(ReadOnlySpan<bool> state, SDL.Keymod modifiers)
        {
            _state = state;
            _modifiers = modifiers;
            _index = -1;
        }

        public Key Current
            => (Key)SDL.GetKeyFromScancode((SDL.Scancode)_index, _modifiers, false);

        public bool MoveNext()
        {
            while (++_index < _state.Length)
            {
                if (_state[_index])
                    return true;
            }
            return false;
        }
    }
}

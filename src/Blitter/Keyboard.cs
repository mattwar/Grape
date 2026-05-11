namespace Blitter;

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

    /// <summary>
    /// Asynchronously waits until the specified virtual <see cref="Key"/> is pressed.
    /// </summary>
    public static async Task WaitForKeyDownAsync(Key key, CancellationToken cancellationToken = default)
    {
        _ = Application.Current;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (IsDown(key))
                return;
            await Task.Yield();
        }
    }

    /// <summary>
    /// Asynchronously waits until the specified <see cref="PhysicalKey"/> is pressed.
    /// </summary>
    public static async Task WaitForPhysicalKeyDownAsync(PhysicalKey physicalKey, CancellationToken cancellationToken = default)
    {
        _ = Application.Current;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (IsDown(physicalKey))
                return;
            await Task.Yield();
        }
    }

    // ---------------- Edge detection ----------------

    // Per-frame snapshots populated by Input.BeginFrame, which runs
    // once per Window.RenderFrame call. WasJustPressed/Released compare
    // these two arrays.
    private static bool[] _currentState = Array.Empty<bool>();
    private static bool[] _previousState = Array.Empty<bool>();

    internal static void BeginFrame()
    {
        var live = SDL.GetKeyboardState(out var numKeys);
        if (_currentState.Length != numKeys)
        {
            _currentState = new bool[numKeys];
            _previousState = new bool[numKeys];
            // First frame: treat the live state as the baseline so no
            // spurious "just pressed" edges fire on startup.
            live.CopyTo(_currentState);
            live.CopyTo(_previousState);
            return;
        }
        // Promote current -> previous, then snapshot live -> current.
        Array.Copy(_currentState, _previousState, numKeys);
        live.CopyTo(_currentState);
    }

    /// <summary>
    /// Returns true only on the frame the physical key transitioned
    /// from up to down. Use this for one-shot actions (jump, fire,
    /// toggle) that should not autorepeat while held.
    /// </summary>
    public static bool WasJustPressed(PhysicalKey physicalKey)
    {
        var idx = (int)physicalKey;
        return (uint)idx < (uint)_currentState.Length
            && _currentState[idx] && !_previousState[idx];
    }

    /// <summary>
    /// Returns true only on the frame the physical key transitioned
    /// from down to up.
    /// </summary>
    public static bool WasJustReleased(PhysicalKey physicalKey)
    {
        var idx = (int)physicalKey;
        return (uint)idx < (uint)_currentState.Length
            && !_currentState[idx] && _previousState[idx];
    }

    /// <inheritdoc cref="WasJustPressed(PhysicalKey)"/>
    public static bool WasJustPressed(Key key) =>
        WasJustPressed((PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)key, out _));

    /// <inheritdoc cref="WasJustReleased(PhysicalKey)"/>
    public static bool WasJustReleased(Key key) =>
        WasJustReleased((PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)key, out _));

    // Test seam: lets the unit tests drive the snapshot state directly
    // instead of standing up an SDL window.
    internal static void SetTestSnapshot(bool[] previous, bool[] current)
    {
        _previousState = previous;
        _currentState = current;
    }
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

using System.Numerics;
using Blitter.Input;

namespace Blitter;

/// <summary>
/// An input snapshot per frame, enabling edge detection for keys and mouse buttons.
/// </summary>
public sealed class FrameInput
{
    private readonly IInputSource _source;

    // Keyboard
    private bool[] _currentKeys = Array.Empty<bool>();
    private bool[] _previousKeys = Array.Empty<bool>();

    // Mouse
    private MouseButtons _currentMouseButtons;
    private MouseButtons _previousMouseButtons;
    private Vector2 _currentMousePosition;
    private Vector2 _previousMousePosition;
    private Vector2 _relativeMouseDelta;
    private bool _hasMouseSnapshot;

    /// <summary>
    /// Creates a new <see cref="FrameInput"/> backed by live SDL input
    /// state. The first call to <see cref="Update"/> establishes the
    /// baseline so no spurious edges are reported on startup.
    /// </summary>
    public FrameInput() : this(new SdlInputSource()) { }

    internal FrameInput(IInputSource source)
    {
        _source = source;
    }

    /// <summary>
    /// Promotes current state to previous, then reads fresh state from
    /// the underlying source. Call once per logical frame, before any
    /// <c>WasJust*</c> or <see cref="MouseDelta"/> query.
    /// </summary>
    public void Update()
    {
        // ----- Keyboard -----
        var numKeys = _source.KeyCount;
        if (_currentKeys.Length != numKeys)
        {
            _currentKeys = new bool[numKeys];
            _previousKeys = new bool[numKeys];
            _source.ReadKeyboardState(_currentKeys);
            // First update: seed both arrays so we don't emit fake
            // "just pressed" edges for keys held at startup.
            Array.Copy(_currentKeys, _previousKeys, numKeys);
        }
        else
        {
            Array.Copy(_currentKeys, _previousKeys, numKeys);
            _source.ReadKeyboardState(_currentKeys);
        }

        // ----- Mouse -----
        _source.ReadMouseState(out var buttons, out var pos);
        // Drain relative-motion accumulator every frame so a later
        // RelativeMouseMode flip doesn't dump backlog into MouseDelta.
        _relativeMouseDelta = _source.ReadRelativeMouseMotion();
        if (!_hasMouseSnapshot)
        {
            _previousMouseButtons = _currentMouseButtons = buttons;
            _previousMousePosition = _currentMousePosition = pos;
            _hasMouseSnapshot = true;
        }
        else
        {
            _previousMouseButtons = _currentMouseButtons;
            _previousMousePosition = _currentMousePosition;
            _currentMouseButtons = buttons;
            _currentMousePosition = pos;
        }
    }

    // -------- Keyboard queries --------

    /// <summary>True if the physical key is currently held.</summary>
    public bool IsDown(PhysicalKey key)
    {
        var idx = (int)key;
        return (uint)idx < (uint)_currentKeys.Length && _currentKeys[idx];
    }

    /// <inheritdoc cref="IsDown(PhysicalKey)"/>
    public bool IsDown(Key key) =>
        IsDown((PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)key, out _));

    /// <summary>
    /// True only on the <see cref="Update"/> tick where the key
    /// transitioned from up to down. Use for one-shot actions
    /// (jump, fire, toggle) that should not autorepeat while held.
    /// </summary>
    public bool WasJustPressed(PhysicalKey key)
    {
        var idx = (int)key;
        return (uint)idx < (uint)_currentKeys.Length
            && _currentKeys[idx] && !_previousKeys[idx];
    }

    /// <summary>
    /// True only on the <see cref="Update"/> tick where the key
    /// transitioned from down to up.
    /// </summary>
    public bool WasJustReleased(PhysicalKey key)
    {
        var idx = (int)key;
        return (uint)idx < (uint)_currentKeys.Length
            && !_currentKeys[idx] && _previousKeys[idx];
    }

    /// <inheritdoc cref="WasJustPressed(PhysicalKey)"/>
    public bool WasJustPressed(Key key) =>
        WasJustPressed((PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)key, out _));

    /// <inheritdoc cref="WasJustReleased(PhysicalKey)"/>
    public bool WasJustReleased(Key key) =>
        WasJustReleased((PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)key, out _));

    /// <summary>
    /// Returns a signed 1D direction from a pair of keys:
    /// <c>+1</c> if <paramref name="positive"/> is held, <c>-1</c> if
    /// <paramref name="negative"/> is held, <c>0</c> if neither or
    /// both are held.
    /// </summary>
    public float Direction(PhysicalKey negative, PhysicalKey positive)
    {
        var n = IsDown(negative) ? 1 : 0;
        var p = IsDown(positive) ? 1 : 0;
        return p - n;
    }

    /// <inheritdoc cref="Direction(PhysicalKey, PhysicalKey)"/>
    public float Direction(Key negative, Key positive) =>
        Direction(
            (PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)negative, out _),
            (PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)positive, out _));

    /// <summary>
    /// Returns a unit-length 2D direction from four keys (e.g. WASD or
    /// arrows). Diagonals are normalized so they don't move faster
    /// than cardinal directions. Returns <see cref="Vector2.Zero"/>
    /// when no keys (or opposing keys) are held.
    /// </summary>
    public Vector2 Direction2D(
        PhysicalKey left, PhysicalKey right,
        PhysicalKey down, PhysicalKey up)
    {
        var v = new Vector2(
            Direction(left, right),
            Direction(down, up));
        var lenSq = v.LengthSquared();
        return lenSq > 0f ? v / MathF.Sqrt(lenSq) : Vector2.Zero;
    }

    /// <inheritdoc cref="Direction2D(PhysicalKey, PhysicalKey, PhysicalKey, PhysicalKey)"/>
    public Vector2 Direction2D(
        Key left, Key right,
        Key down, Key up) =>
        Direction2D(
            (PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)left, out _),
            (PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)right, out _),
            (PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)down, out _),
            (PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)up, out _));

    // -------- Mouse queries --------

    /// <summary>True if the mouse button is currently held.</summary>
    public bool IsDown(MouseButton button)
    {
        var bit = (uint)button;
        if (bit == 0) return false;
        var mask = (MouseButtons)(1u << ((int)bit - 1));
        return (_currentMouseButtons & mask) != 0;
    }

    /// <inheritdoc cref="WasJustPressed(PhysicalKey)"/>
    public bool WasJustPressed(MouseButton button)
    {
        var bit = (uint)button;
        if (bit == 0) return false;
        var mask = (MouseButtons)(1u << ((int)bit - 1));
        return (_currentMouseButtons & mask) != 0
            && (_previousMouseButtons & mask) == 0;
    }

    /// <inheritdoc cref="WasJustReleased(PhysicalKey)"/>
    public bool WasJustReleased(MouseButton button)
    {
        var bit = (uint)button;
        if (bit == 0) return false;
        var mask = (MouseButtons)(1u << ((int)bit - 1));
        return (_currentMouseButtons & mask) == 0
            && (_previousMouseButtons & mask) != 0;
    }

    /// <summary>
    /// Cursor movement since the previous <see cref="Update"/> tick.
    /// When any window has <see cref="Window.RelativeMouseMode"/> on,
    /// returns SDL's relative-motion delta (keeps reporting motion
    /// even though the cursor is pinned); otherwise returns the
    /// difference of the desktop cursor position between snapshots.
    /// </summary>
    public Vector2 MouseDelta =>
        _source.IsAnyWindowRelativeMouseMode()
            ? _relativeMouseDelta
            : _currentMousePosition - _previousMousePosition;

    /// <summary>
    /// The cursor position snapshotted at the last <see cref="Update"/>
    /// (desktop / global coordinates).
    /// </summary>
    public Vector2 MousePosition => _currentMousePosition;

    // -------- Internal previous-state accessors --------
    // Used by InputActions to compute action-level edges (action
    // transitions, rather than per-binding OR'd rising edges, which
    // would double-fire when multiple bindings rise simultaneously).

    internal bool WasPreviouslyDown(PhysicalKey key)
    {
        var idx = (int)key;
        return (uint)idx < (uint)_previousKeys.Length && _previousKeys[idx];
    }

    internal bool WasPreviouslyDown(Key key) =>
        WasPreviouslyDown((PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)key, out _));

    internal bool WasPreviouslyDown(MouseButton button)
    {
        var bit = (uint)button;
        if (bit == 0) return false;
        var mask = (MouseButtons)(1u << ((int)bit - 1));
        return (_previousMouseButtons & mask) != 0;
    }

    /// <summary>
    /// Computes the previous-frame value of <see cref="Direction(PhysicalKey, PhysicalKey)"/>.
    /// </summary>
    internal float PreviousDirection(PhysicalKey negative, PhysicalKey positive)
    {
        var n = WasPreviouslyDown(negative) ? 1 : 0;
        var p = WasPreviouslyDown(positive) ? 1 : 0;
        return p - n;
    }

    internal float PreviousDirection(Key negative, Key positive) =>
        PreviousDirection(
            (PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)negative, out _),
            (PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)positive, out _));

    internal Vector2 PreviousDirection2D(
        PhysicalKey left, PhysicalKey right,
        PhysicalKey down, PhysicalKey up)
    {
        var v = new Vector2(
            PreviousDirection(left, right),
            PreviousDirection(down, up));
        var lenSq = v.LengthSquared();
        return lenSq > 0f ? v / MathF.Sqrt(lenSq) : Vector2.Zero;
    }

    internal Vector2 PreviousDirection2D(
        Key left, Key right,
        Key down, Key up) =>
        PreviousDirection2D(
            (PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)left, out _),
            (PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)right, out _),
            (PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)down, out _),
            (PhysicalKey)SDL.GetScancodeFromKey((SDL.Keycode)up, out _));
}

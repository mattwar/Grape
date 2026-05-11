using System.Numerics;

namespace Blitter;

/// <summary>
/// Provides access to global mouse / cursor state.
/// </summary>
public static class Mouse
{
    /// <summary>
    /// Gets or sets whether the mouse cursor is visible.
    /// </summary>
    public static bool IsVisible
    {
        get
        {
            _ = Application.Current;
            return SDL.CursorVisible();
        }

        set
        {
            _ = Application.Current;
            if (value)
                SDL.ShowCursor();
            else
                SDL.HideCursor();
        }
    }

    /// <summary>
    /// True if at least one mouse (or mouse-like input device) is currently connected.
    /// </summary>
    public static bool HasMouse
    {
        get
        {
            _ = Application.Current;
            return SDL.HasMouse();
        }
    }

    /// <summary>
    /// The current mouse button state.
    /// </summary>
    public static MouseButtons Buttons
    {
        get
        {
            _ = Application.Current;
            return (MouseButtons)SDL.GetGlobalMouseState(out _, out _);
        }
    }

    /// <summary>
    /// Returns true if the given mouse button is currently held down.
    /// </summary>
    public static bool IsDown(MouseButton button)
    {
        var bit = (uint)button;
        if (bit == 0)
            return false;
        var mask = (MouseButtons)(1u << ((int)bit - 1));
        return (Buttons & mask) != 0;
    }

    /// <summary>
    /// The mouse cursor position in desktop (global) coordinates.
    /// Setting this warps the cursor to the requested position.
    /// While any window has <see cref="Window.RelativeMouseMode"/>
    /// enabled the OS pins the cursor and this value stops moving;
    /// use <see cref="Delta"/> for motion in that mode.
    /// </summary>
    public static Vector2 Position
    {
        get
        {
            _ = Application.Current;
            SDL.GetGlobalMouseState(out var x, out var y);
            return new Vector2(x, y);
        }

        set
        {
            _ = Application.Current;
            SDL.WarpMouseGlobal(value.X, value.Y);
        }
    }

    /// <summary>
    /// Returns the mouse cursor position in coordinates relative to the
    /// top-left of the given window. Pinned (does not move) while any
    /// window has <see cref="Window.RelativeMouseMode"/> enabled; use
    /// <see cref="Delta"/> for motion in that mode.
    /// </summary>
    public static Vector2 GetPosition(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _ = Application.Current;
        SDL.GetGlobalMouseState(out var gx, out var gy);
        var (wx, wy) = window.Position;
        return new Vector2(gx - wx, gy - wy);
    }

    /// <summary>
    /// Warps the mouse cursor to the given position in coordinates relative
    /// to the top-left of the given window.
    /// </summary>
    public static void SetPosition(Window window, Vector2 position)
    {
        ArgumentNullException.ThrowIfNull(window);
        _ = Application.Current;
        SDL.WarpMouseInWindow(window.WindowId, position.X, position.Y);
    }

    // ---------------- Edge detection / per-frame deltas ----------------

    private static MouseButtons _currentButtons;
    private static MouseButtons _previousButtons;
    private static Vector2 _currentPosition;
    private static Vector2 _previousPosition;
    private static Vector2 _relativeDelta;
    private static bool _hasFrameSnapshot;

    internal static void BeginFrame()
    {
        var buttons = (MouseButtons)SDL.GetGlobalMouseState(out var x, out var y);
        var pos = new Vector2(x, y);
        // Always drain SDL's relative-state accumulator so a later
        // RelativeMouseMode = true doesn't dump backlog into Delta.
        SDL.GetRelativeMouseState(out var rx, out var ry);
        _relativeDelta = new Vector2(rx, ry);
        if (!_hasFrameSnapshot)
        {
            _previousButtons = _currentButtons = buttons;
            _previousPosition = _currentPosition = pos;
            _hasFrameSnapshot = true;
            return;
        }
        _previousButtons = _currentButtons;
        _previousPosition = _currentPosition;
        _currentButtons = buttons;
        _currentPosition = pos;
    }

    /// <summary>
    /// Cursor movement since the previous frame, in pixels.
    /// When any window has <see cref="Window.RelativeMouseMode"/> on,
    /// returns SDL's relative-motion delta (keeps reporting motion even though the cursor is pinned). 
    /// Otherwise returns the difference of the desktop cursor position between frames.
    /// </summary>
    public static Vector2 Delta =>
        IsAnyWindowRelative() ? _relativeDelta
                              : _currentPosition - _previousPosition;

    private static bool IsAnyWindowRelative()
    {
        var windows = Application.Current.Windows;
        for (var i = 0; i < windows.Count; i++)
        {
            if (windows[i].RelativeMouseMode) return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true only on the frame the given mouse button
    /// transitioned from up to down.
    /// </summary>
    public static bool WasJustPressed(MouseButton button)
    {
        var bit = (uint)button;
        if (bit == 0) return false;
        var mask = (MouseButtons)(1u << ((int)bit - 1));
        return (_currentButtons & mask) != 0 && (_previousButtons & mask) == 0;
    }

    /// <summary>
    /// Returns true only on the frame the given mouse button
    /// transitioned from down to up.
    /// </summary>
    public static bool WasJustReleased(MouseButton button)
    {
        var bit = (uint)button;
        if (bit == 0) return false;
        var mask = (MouseButtons)(1u << ((int)bit - 1));
        return (_currentButtons & mask) == 0 && (_previousButtons & mask) != 0;
    }

    // Test seam: lets the unit tests drive the snapshot state directly
    // instead of standing up an SDL window.
    internal static void SetTestSnapshot(
        MouseButtons previousButtons, MouseButtons currentButtons,
        Vector2 previousPosition, Vector2 currentPosition)
    {
        _previousButtons = previousButtons;
        _currentButtons = currentButtons;
        _previousPosition = previousPosition;
        _currentPosition = currentPosition;
        _hasFrameSnapshot = true;
    }
}

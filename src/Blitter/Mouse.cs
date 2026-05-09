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
    /// top-left of the given window.
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
}

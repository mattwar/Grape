namespace SDL3.Model;

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
}

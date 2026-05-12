using System.Numerics;

namespace Blitter.Input;

/// <summary>
/// SDL-backed live state source for <see cref="FrameInput"/>.
/// </summary>
internal sealed class SdlInputSource : IInputSource
{
    public int KeyCount
    {
        get
        {
            _ = Application.Current;
            _ = SDL.GetKeyboardState(out var numKeys);
            return numKeys;
        }
    }

    public void ReadKeyboardState(Span<bool> destination)
    {
        _ = Application.Current;
        var live = SDL.GetKeyboardState(out var numKeys);
        if (numKeys > destination.Length)
            numKeys = destination.Length;
        live[..numKeys].CopyTo(destination);
    }

    public void ReadMouseState(out MouseButtons buttons, out Vector2 position)
    {
        _ = Application.Current;
        buttons = (MouseButtons)SDL.GetGlobalMouseState(out var x, out var y);
        position = new Vector2(x, y);
    }

    public Vector2 ReadRelativeMouseMotion()
    {
        _ = Application.Current;
        SDL.GetRelativeMouseState(out var rx, out var ry);
        return new Vector2(rx, ry);
    }

    public bool IsAnyWindowRelativeMouseMode()
    {
        var windows = Application.Current.Windows;
        for (var i = 0; i < windows.Count; i++)
        {
            if (windows[i].RelativeMouseMode) return true;
        }
        return false;
    }
}

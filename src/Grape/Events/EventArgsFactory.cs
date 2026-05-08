using System.Numerics;
using System.Runtime.InteropServices;

namespace Grape.Events;

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

using System.Numerics;
using Blitter.Input;

namespace Blitter.Tests;

/// <summary>
/// Test double for <see cref="IInputSource"/> that the unit tests
/// drive directly instead of standing up SDL.
/// </summary>
internal sealed class FakeInputSource : IInputSource
{
    public bool[] Keys { get; set; } = new bool[512];
    public MouseButtons MouseButtons { get; set; }
    public Vector2 MousePosition { get; set; }
    public Vector2 RelativeMouseMotion { get; set; }
    public bool AnyWindowRelative { get; set; }

    public int KeyCount => Keys.Length;

    public void ReadKeyboardState(Span<bool> destination)
    {
        Keys.AsSpan(0, Math.Min(Keys.Length, destination.Length))
            .CopyTo(destination);
    }

    public void ReadMouseState(out MouseButtons buttons, out Vector2 position)
    {
        buttons = MouseButtons;
        position = MousePosition;
    }

    public Vector2 ReadRelativeMouseMotion() => RelativeMouseMotion;

    public bool IsAnyWindowRelativeMouseMode() => AnyWindowRelative;
}

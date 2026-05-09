using System.Numerics;

namespace Blitter.Events;

public readonly record struct MouseMoveEventArgs(
    Vector2 Position,
    Vector2 Delta,
    MouseButtons Buttons);

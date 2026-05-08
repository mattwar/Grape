using System.Numerics;

namespace Grape.Events;

public readonly record struct MouseMoveEventArgs(
    Vector2 Position,
    Vector2 Delta,
    MouseButtons Buttons);

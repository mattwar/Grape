using System.Numerics;

namespace Grape.Events;

public readonly record struct MouseWheelEventArgs(
    Vector2 Scroll,
    Vector2 MousePosition,
    MouseWheelDirection Direction);

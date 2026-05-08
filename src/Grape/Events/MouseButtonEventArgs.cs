using System.Numerics;

namespace Grape.Events;

public readonly record struct MouseButtonEventArgs(
    MouseButton Button,
    bool IsDown,
    int Clicks,
    Vector2 Position);

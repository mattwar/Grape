using System.Numerics;

namespace Blitter.Events;

public readonly record struct MouseButtonEventArgs(
    MouseButton Button,
    bool IsDown,
    int Clicks,
    Vector2 Position);

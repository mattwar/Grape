using SDL3;

namespace Grape.Vine;

public struct UpdateContext
{
    public TimeSpan Time { get; init; }
    public SDL.Rect Bounds { get; init; }
}

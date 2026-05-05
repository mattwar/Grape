using SDL3;

namespace Grape.Vine;

public struct UpdateContext
{
    public TimeSpan ElapsedSinceStart { get; init; }

    public TimeSpan ElaspsedSinceLastUpdate { get; init; }

    public SDL.Rect Bounds { get; init; }
}

namespace Grape.Jelly;

public struct UpdateContext
{
    public TimeSpan ElapsedSinceStart { get; init; }

    public TimeSpan ElaspsedSinceLastUpdate { get; init; }

    public Rect Bounds { get; init; }
}

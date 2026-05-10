using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// An axis-aligned bounding circle.
/// </summary>
public readonly struct BoundingCircle : IEquatable<BoundingCircle>
{
    public Vector2 Center { get; }
    public float Radius { get; }

    public BoundingCircle(Vector2 center, float radius)
    {
        Center = center;
        Radius = radius;
    }

    public static BoundingCircle Empty { get; } = new(Vector2.Zero, -1f);

    public bool IsEmpty => Radius < 0f;

    /// <summary>
    /// Builds a <see cref="BoundingCircle"/> from a set of points.
    /// </summary>
    public static BoundingCircle FromPoints(ReadOnlySpan<Vector2> points)
    {
        if (points.IsEmpty) return Empty;
        var center = Vector2.Zero;
        for (int i = 0; i < points.Length; i++)
            center += points[i];
        center /= points.Length;

        float maxSq = 0f;
        for (int i = 0; i < points.Length; i++)
        {
            float d = Vector2.DistanceSquared(center, points[i]);
            if (d > maxSq) maxSq = d;
        }
        return new BoundingCircle(center, MathF.Sqrt(maxSq));
    }

    /// <summary>
    /// Builds a <see cref="BoundingCircle"/> from a set of vertices.
    /// </summary>
    public static BoundingCircle FromVertices<TVertex>(ReadOnlySpan<TVertex> vertices)
        where TVertex : unmanaged, IPositionVertex2D
    {
        if (vertices.IsEmpty) return Empty;
        var center = Vector2.Zero;
        for (int i = 0; i < vertices.Length; i++)
            center += vertices[i].Position;
        center /= vertices.Length;

        float maxSq = 0f;
        for (int i = 0; i < vertices.Length; i++)
        {
            float d = Vector2.DistanceSquared(center, vertices[i].Position);
            if (d > maxSq) maxSq = d;
        }
        return new BoundingCircle(center, MathF.Sqrt(maxSq));
    }

    /// <summary>
    /// The bounding circle that enclosed the bounding rectangle.
    /// </summary>
    public static BoundingCircle FromRect(BoundingRect rect) =>
        rect.IsEmpty
            ? Empty
            : new BoundingCircle(rect.Center, rect.Extents.Length());

    public bool Contains(Vector2 point) =>
        !IsEmpty && Vector2.DistanceSquared(Center, point) <= Radius * Radius;

    public bool Intersects(BoundingCircle other)
    {
        if (IsEmpty || other.IsEmpty) return false;
        float r = Radius + other.Radius;
        return Vector2.DistanceSquared(Center, other.Center) <= r * r;
    }

    public bool Intersects(BoundingRect rect)
    {
        if (IsEmpty || rect.IsEmpty) return false;
        var clamped = Vector2.Clamp(Center, rect.Min, rect.Max);
        return Vector2.DistanceSquared(Center, clamped) <= Radius * Radius;
    }

    public BoundingCircle Encapsulate(Vector2 point)
    {
        if (IsEmpty) return new BoundingCircle(point, 0f);
        var d = point - Center;
        float distSq = d.LengthSquared();
        if (distSq <= Radius * Radius) return this;

        // Grow just enough to include the point: new circle passes through
        // the old's far side and the new point.
        float dist = MathF.Sqrt(distSq);
        float newRadius = (Radius + dist) * 0.5f;
        var newCenter = Center + d * ((newRadius - Radius) / dist);
        return new BoundingCircle(newCenter, newRadius);
    }

    /// <summary>
    /// Returns the <see cref="BoundingCircle"/> that bounds this circle after applying the transform <paramref name="matrix"/>.
    /// </summary>
    public BoundingCircle Transform(Matrix3x2 matrix)
    {
        if (IsEmpty) return Empty;
        var newCenter = Vector2.Transform(Center, matrix);
        float sx = new Vector2(matrix.M11, matrix.M12).Length();
        float sy = new Vector2(matrix.M21, matrix.M22).Length();
        float maxScale = MathF.Max(sx, sy);
        return new BoundingCircle(newCenter, Radius * maxScale);
    }

    public bool Equals(BoundingCircle other) => Center.Equals(other.Center) && Radius == other.Radius;
    public override bool Equals(object? obj) => obj is BoundingCircle c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(Center, Radius);
    public static bool operator ==(BoundingCircle a, BoundingCircle b) => a.Equals(b);
    public static bool operator !=(BoundingCircle a, BoundingCircle b) => !a.Equals(b);
    public override string ToString() => IsEmpty ? "BoundingCircle(Empty)" : $"BoundingCircle(Center={Center}, Radius={Radius})";
}

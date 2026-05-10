using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// A sphere defined by center and radius. A negative <see cref="Radius"/>
/// (specifically the sentinel value used by <see cref="Empty"/>) means the
/// sphere holds no points.
/// </summary>
public readonly struct BoundingSphere : IEquatable<BoundingSphere>
{
    public Vector3 Center { get; }
    public float Radius { get; }

    public BoundingSphere(Vector3 center, float radius)
    {
        Center = center;
        Radius = radius;
    }

    /// <summary>The sentinel "empty" sphere (negative radius).</summary>
    public static BoundingSphere Empty { get; } = new(Vector3.Zero, -1f);

    public bool IsEmpty => Radius < 0f;

    /// <summary>
    /// Builds a sphere from a span of points using a fast two-pass approach:
    /// pick the centroid, then take the radius as the distance to the
    /// farthest point. Not the minimum-bounding sphere, but it's stable,
    /// allocation-free, and within a small constant factor of optimal.
    /// </summary>
    public static BoundingSphere FromPoints(ReadOnlySpan<Vector3> points)
    {
        if (points.IsEmpty) return Empty;
        var center = Vector3.Zero;
        for (int i = 0; i < points.Length; i++)
            center += points[i];
        center /= points.Length;

        float maxSq = 0f;
        for (int i = 0; i < points.Length; i++)
        {
            float d = Vector3.DistanceSquared(center, points[i]);
            if (d > maxSq) maxSq = d;
        }
        return new BoundingSphere(center, MathF.Sqrt(maxSq));
    }

    public static BoundingSphere FromVertices<TVertex>(ReadOnlySpan<TVertex> vertices)
        where TVertex : unmanaged, IPositionVertex3D
    {
        if (vertices.IsEmpty) return Empty;
        var center = Vector3.Zero;
        for (int i = 0; i < vertices.Length; i++)
            center += vertices[i].Position;
        center /= vertices.Length;

        float maxSq = 0f;
        for (int i = 0; i < vertices.Length; i++)
        {
            float d = Vector3.DistanceSquared(center, vertices[i].Position);
            if (d > maxSq) maxSq = d;
        }
        return new BoundingSphere(center, MathF.Sqrt(maxSq));
    }

    /// <summary>The sphere that snugly encloses <paramref name="box"/>.</summary>
    public static BoundingSphere FromBox(BoundingBox box) =>
        box.IsEmpty
            ? Empty
            : new BoundingSphere(box.Center, box.Extents.Length());

    public bool Contains(Vector3 point) =>
        !IsEmpty && Vector3.DistanceSquared(Center, point) <= Radius * Radius;

    public bool Intersects(BoundingSphere other)
    {
        if (IsEmpty || other.IsEmpty) return false;
        float r = Radius + other.Radius;
        return Vector3.DistanceSquared(Center, other.Center) <= r * r;
    }

    public bool Intersects(BoundingBox box)
    {
        if (IsEmpty || box.IsEmpty) return false;
        var clamped = Vector3.Clamp(Center, box.Min, box.Max);
        return Vector3.DistanceSquared(Center, clamped) <= Radius * Radius;
    }

    public BoundingSphere Encapsulate(Vector3 point)
    {
        if (IsEmpty) return new BoundingSphere(point, 0f);
        var d = point - Center;
        float distSq = d.LengthSquared();
        if (distSq <= Radius * Radius) return this;

        // Grow just enough to include the point: new sphere passes through
        // the old's far side and the new point.
        float dist = MathF.Sqrt(distSq);
        float newRadius = (Radius + dist) * 0.5f;
        var newCenter = Center + d * ((newRadius - Radius) / dist);
        return new BoundingSphere(newCenter, newRadius);
    }

    /// <summary>
    /// Returns the sphere that bounds this one after applying
    /// <paramref name="matrix"/>. Translation/rotation move the center;
    /// scale grows the radius by the largest absolute axis scale (so the
    /// result still bounds the geometry under non-uniform scale, just not
    /// minimally).
    /// </summary>
    public BoundingSphere Transform(Matrix4x4 matrix)
    {
        if (IsEmpty) return Empty;
        var newCenter = Vector3.Transform(Center, matrix);
        float sx = new Vector3(matrix.M11, matrix.M12, matrix.M13).Length();
        float sy = new Vector3(matrix.M21, matrix.M22, matrix.M23).Length();
        float sz = new Vector3(matrix.M31, matrix.M32, matrix.M33).Length();
        float maxScale = MathF.Max(sx, MathF.Max(sy, sz));
        return new BoundingSphere(newCenter, Radius * maxScale);
    }

    public bool Equals(BoundingSphere other) => Center.Equals(other.Center) && Radius == other.Radius;
    public override bool Equals(object? obj) => obj is BoundingSphere s && Equals(s);
    public override int GetHashCode() => HashCode.Combine(Center, Radius);
    public static bool operator ==(BoundingSphere a, BoundingSphere b) => a.Equals(b);
    public static bool operator !=(BoundingSphere a, BoundingSphere b) => !a.Equals(b);
    public override string ToString() => IsEmpty ? "BoundingSphere(Empty)" : $"BoundingSphere(Center={Center}, Radius={Radius})";
}

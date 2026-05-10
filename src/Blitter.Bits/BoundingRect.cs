using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// An axis-aligned 2D bounding rectangle.
/// </summary>
public readonly struct BoundingRect : IEquatable<BoundingRect>
{
    public Vector2 Min { get; }
    public Vector2 Max { get; }

    public BoundingRect(Vector2 min, Vector2 max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>
    /// The sentinel "empty" rectangle.
    /// </summary>
    public static BoundingRect Empty { get; } = new(
        new Vector2(float.PositiveInfinity),
        new Vector2(float.NegativeInfinity));

    public bool IsEmpty => Min.X > Max.X || Min.Y > Max.Y;

    public Vector2 Center => (Min + Max) * 0.5f;
    public Vector2 Size => Max - Min;
    public Vector2 Extents => (Max - Min) * 0.5f;

    public static BoundingRect FromCenterSize(Vector2 center, Vector2 size)
    {
        var half = size * 0.5f;
        return new BoundingRect(center - half, center + half);
    }

    public static BoundingRect FromPoints(ReadOnlySpan<Vector2> points)
    {
        if (points.IsEmpty) return Empty;
        var min = points[0];
        var max = min;
        for (int i = 1; i < points.Length; i++)
        {
            min = Vector2.Min(min, points[i]);
            max = Vector2.Max(max, points[i]);
        }
        return new BoundingRect(min, max);
    }

    /// <summary>
    /// Builds a <see cref="BoundingRect"/> from a set of vertices.
    /// </summary>
    public static BoundingRect FromVertices<TVertex>(ReadOnlySpan<TVertex> vertices)
        where TVertex : unmanaged, IPositionVertex2D
    {
        if (vertices.IsEmpty) return Empty;
        var min = vertices[0].Position;
        var max = min;
        for (int i = 1; i < vertices.Length; i++)
        {
            var p = vertices[i].Position;
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return new BoundingRect(min, max);
    }

    /// <summary>
    /// Builds a <see cref="BoundingRect"/> from a <see cref="Rect"/>.
    /// </summary>
    public static BoundingRect FromRect(Rect rect) =>
        new(new Vector2(rect.Left, rect.Top), new Vector2(rect.Right, rect.Bottom));

    /// <summary>Convert back to the screen-space <see cref="Rect"/> layout type.</summary>
    public Rect ToRect() => new(Min.X, Min.Y, Max.X - Min.X, Max.Y - Min.Y);

    public BoundingRect Encapsulate(Vector2 point) =>
        IsEmpty
            ? new BoundingRect(point, point)
            : new BoundingRect(Vector2.Min(Min, point), Vector2.Max(Max, point));

    public BoundingRect Encapsulate(BoundingRect other)
    {
        if (other.IsEmpty) return this;
        if (IsEmpty) return other;
        return new BoundingRect(Vector2.Min(Min, other.Min), Vector2.Max(Max, other.Max));
    }

    public bool Contains(Vector2 point) =>
        point.X >= Min.X && point.X <= Max.X &&
        point.Y >= Min.Y && point.Y <= Max.Y;

    public bool Contains(BoundingRect other) =>
        !other.IsEmpty &&
        other.Min.X >= Min.X && other.Max.X <= Max.X &&
        other.Min.Y >= Min.Y && other.Max.Y <= Max.Y;

    public bool Intersects(BoundingRect other) =>
        !IsEmpty && !other.IsEmpty &&
        Min.X <= other.Max.X && Max.X >= other.Min.X &&
        Min.Y <= other.Max.Y && Max.Y >= other.Min.Y;

    /// <summary>
    /// Returns the bounding rect that encloses this rect after applying
    /// <paramref name="matrix"/>. Transforms all four corners (correct
    /// under any 2D affine matrix, including non-uniform scale and
    /// rotation; the result is again axis-aligned).
    /// </summary>
    public BoundingRect Transform(Matrix3x2 matrix)
    {
        if (IsEmpty) return Empty;

        // Four corners (CCW from min); transforming Min/Max alone would
        // only be right for axis-aligned scale + translation.
        Span<Vector2> corners = stackalloc Vector2[4];
        corners[0] = new Vector2(Min.X, Min.Y);
        corners[1] = new Vector2(Max.X, Min.Y);
        corners[2] = new Vector2(Max.X, Max.Y);
        corners[3] = new Vector2(Min.X, Max.Y);

        var min = Vector2.Transform(corners[0], matrix);
        var max = min;
        for (int i = 1; i < 4; i++)
        {
            var p = Vector2.Transform(corners[i], matrix);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return new BoundingRect(min, max);
    }

    public bool Equals(BoundingRect other) => Min.Equals(other.Min) && Max.Equals(other.Max);
    public override bool Equals(object? obj) => obj is BoundingRect r && Equals(r);
    public override int GetHashCode() => HashCode.Combine(Min, Max);
    public static bool operator ==(BoundingRect a, BoundingRect b) => a.Equals(b);
    public static bool operator !=(BoundingRect a, BoundingRect b) => !a.Equals(b);
    public override string ToString() => IsEmpty ? "BoundingRect(Empty)" : $"BoundingRect(Min={Min}, Max={Max})";
}

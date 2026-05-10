using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Axis-aligned bounding box (AABB) defined by its min and max corners.
/// Empty boxes use a sentinel <see cref="Min"/> &gt; <see cref="Max"/> so
/// that <see cref="Encapsulate(Vector3)"/> on an empty box yields a degenerate
/// box at the supplied point.
/// </summary>
public readonly struct BoundingBox : IEquatable<BoundingBox>
{
    public Vector3 Min { get; }
    public Vector3 Max { get; }

    public BoundingBox(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>
    /// The sentinel "empty" box: <see cref="Min"/> = +infinity,
    /// <see cref="Max"/> = -infinity. Use as the seed when accumulating
    /// bounds with <see cref="Encapsulate(Vector3)"/>.
    /// </summary>
    public static BoundingBox Empty { get; } = new(
        new Vector3(float.PositiveInfinity),
        new Vector3(float.NegativeInfinity));

    /// <summary>True when no points have been added (sentinel state).</summary>
    public bool IsEmpty => Min.X > Max.X || Min.Y > Max.Y || Min.Z > Max.Z;

    /// <summary>Center of the box. Undefined for an empty box.</summary>
    public Vector3 Center => (Min + Max) * 0.5f;

    /// <summary>Width/height/depth (Max - Min). Zero for an empty box's components.</summary>
    public Vector3 Size => Max - Min;

    /// <summary>Half of <see cref="Size"/>.</summary>
    public Vector3 Extents => (Max - Min) * 0.5f;

    public static BoundingBox FromCenterSize(Vector3 center, Vector3 size)
    {
        var half = size * 0.5f;
        return new BoundingBox(center - half, center + half);
    }

    public static BoundingBox FromPoints(ReadOnlySpan<Vector3> points)
    {
        if (points.IsEmpty) return Empty;
        var min = points[0];
        var max = min;
        for (int i = 1; i < points.Length; i++)
        {
            min = Vector3.Min(min, points[i]);
            max = Vector3.Max(max, points[i]);
        }
        return new BoundingBox(min, max);
    }

    /// <summary>
    /// Builds an AABB from a vertex span. Works with any vertex struct that
    /// implements <see cref="IPositionVertex3D"/>.
    /// </summary>
    public static BoundingBox FromVertices<TVertex>(ReadOnlySpan<TVertex> vertices)
        where TVertex : unmanaged, IPositionVertex3D
    {
        if (vertices.IsEmpty) return Empty;
        var min = vertices[0].Position;
        var max = min;
        for (int i = 1; i < vertices.Length; i++)
        {
            var p = vertices[i].Position;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return new BoundingBox(min, max);
    }

    /// <summary>Returns a box that includes <paramref name="point"/>.</summary>
    public BoundingBox Encapsulate(Vector3 point) =>
        IsEmpty
            ? new BoundingBox(point, point)
            : new BoundingBox(Vector3.Min(Min, point), Vector3.Max(Max, point));

    /// <summary>Returns a box that includes <paramref name="other"/>.</summary>
    public BoundingBox Encapsulate(BoundingBox other)
    {
        if (other.IsEmpty) return this;
        if (IsEmpty) return other;
        return new BoundingBox(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));
    }

    public bool Contains(Vector3 point) =>
        point.X >= Min.X && point.X <= Max.X &&
        point.Y >= Min.Y && point.Y <= Max.Y &&
        point.Z >= Min.Z && point.Z <= Max.Z;

    public bool Contains(BoundingBox other) =>
        !other.IsEmpty &&
        other.Min.X >= Min.X && other.Max.X <= Max.X &&
        other.Min.Y >= Min.Y && other.Max.Y <= Max.Y &&
        other.Min.Z >= Min.Z && other.Max.Z <= Max.Z;

    public bool Intersects(BoundingBox other) =>
        !IsEmpty && !other.IsEmpty &&
        Min.X <= other.Max.X && Max.X >= other.Min.X &&
        Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
        Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;

    /// <summary>
    /// Returns the AABB that bounds this box after applying
    /// <paramref name="matrix"/>. Computes by transforming all eight corners
    /// (correct for any affine matrix, including non-uniform scale and
    /// rotation).
    /// </summary>
    public BoundingBox Transform(Matrix4x4 matrix)
    {
        if (IsEmpty) return Empty;

        // Transform corners individually rather than transforming Min/Max
        // (which would only be correct for axis-aligned scale + translation).
        Span<Vector3> corners = stackalloc Vector3[8];
        corners[0] = new Vector3(Min.X, Min.Y, Min.Z);
        corners[1] = new Vector3(Max.X, Min.Y, Min.Z);
        corners[2] = new Vector3(Min.X, Max.Y, Min.Z);
        corners[3] = new Vector3(Max.X, Max.Y, Min.Z);
        corners[4] = new Vector3(Min.X, Min.Y, Max.Z);
        corners[5] = new Vector3(Max.X, Min.Y, Max.Z);
        corners[6] = new Vector3(Min.X, Max.Y, Max.Z);
        corners[7] = new Vector3(Max.X, Max.Y, Max.Z);

        var min = Vector3.Transform(corners[0], matrix);
        var max = min;
        for (int i = 1; i < 8; i++)
        {
            var p = Vector3.Transform(corners[i], matrix);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return new BoundingBox(min, max);
    }

    public bool Equals(BoundingBox other) => Min.Equals(other.Min) && Max.Equals(other.Max);
    public override bool Equals(object? obj) => obj is BoundingBox b && Equals(b);
    public override int GetHashCode() => HashCode.Combine(Min, Max);
    public static bool operator ==(BoundingBox a, BoundingBox b) => a.Equals(b);
    public static bool operator !=(BoundingBox a, BoundingBox b) => !a.Equals(b);
    public override string ToString() => IsEmpty ? "BoundingBox(Empty)" : $"BoundingBox(Min={Min}, Max={Max})";
}

using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Extension methods for working with spans of <see cref="BoundingBox"/>es.
/// </summary>
public static class BoundingBoxesExtensions
{
    /// <summary>True if any box in <paramref name="boxes"/> contains <paramref name="point"/>.</summary>
    public static bool ContainsAny(this ReadOnlySpan<BoundingBox> boxes, Vector3 point)
    {
        for (int i = 0; i < boxes.Length; i++)
            if (boxes[i].Contains(point)) return true;
        return false;
    }

    /// <summary>True if any box in <paramref name="boxes"/> intersects <paramref name="other"/>.</summary>
    public static bool IntersectsAny(this ReadOnlySpan<BoundingBox> boxes, BoundingBox other)
    {
        for (int i = 0; i < boxes.Length; i++)
            if (boxes[i].Intersects(other)) return true;
        return false;
    }

    /// <summary>True if any box in <paramref name="a"/> intersects any box in <paramref name="b"/>.</summary>
    public static bool IntersectsAny(this ReadOnlySpan<BoundingBox> a, ReadOnlySpan<BoundingBox> b)
    {
        for (int i = 0; i < a.Length; i++)
        {
            var ba = a[i];
            for (int j = 0; j < b.Length; j++)
                if (ba.Intersects(b[j])) return true;
        }
        return false;
    }

    /// <summary>
    /// Single <see cref="BoundingBox"/> that encloses every box in
    /// <paramref name="boxes"/>. Returns <see cref="BoundingBox.Empty"/>
    /// if the span is empty.
    /// </summary>
    public static BoundingBox Union(this ReadOnlySpan<BoundingBox> boxes)
    {
        var result = BoundingBox.Empty;
        for (int i = 0; i < boxes.Length; i++)
            result = result.Encapsulate(boxes[i]);
        return result;
    }
}

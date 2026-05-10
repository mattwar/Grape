using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Extension methods for working with spans of <see cref="BoundingRect"/>s.
/// </summary>
public static class BoundingRectsExtensions
{
    /// <summary>True if any rect in <paramref name="rects"/> contains <paramref name="point"/>.</summary>
    public static bool ContainsAny(this ReadOnlySpan<BoundingRect> rects, Vector2 point)
    {
        for (int i = 0; i < rects.Length; i++)
            if (rects[i].Contains(point)) return true;
        return false;
    }

    /// <summary>True if any rect in <paramref name="rects"/> intersects <paramref name="other"/>.</summary>
    public static bool IntersectsAny(this ReadOnlySpan<BoundingRect> rects, BoundingRect other)
    {
        for (int i = 0; i < rects.Length; i++)
            if (rects[i].Intersects(other)) return true;
        return false;
    }

    /// <summary>True if any rect in <paramref name="a"/> intersects any rect in <paramref name="b"/>.</summary>
    public static bool IntersectsAny(this ReadOnlySpan<BoundingRect> a, ReadOnlySpan<BoundingRect> b)
    {
        for (int i = 0; i < a.Length; i++)
        {
            var ra = a[i];
            for (int j = 0; j < b.Length; j++)
                if (ra.Intersects(b[j])) return true;
        }
        return false;
    }

    /// <summary>
    /// Single <see cref="BoundingRect"/> that encloses every rect in
    /// <paramref name="rects"/>. Returns <see cref="BoundingRect.Empty"/>
    /// if the span is empty.
    /// </summary>
    public static BoundingRect Union(this ReadOnlySpan<BoundingRect> rects)
    {
        var result = BoundingRect.Empty;
        for (int i = 0; i < rects.Length; i++)
            result = result.Encapsulate(rects[i]);
        return result;
    }
}

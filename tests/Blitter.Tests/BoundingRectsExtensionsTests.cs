using System.Numerics;
using Blitter.Bits;

namespace Blitter.Tests;

public class BoundingRectsExtensionsTests
{
    private static readonly BoundingRect[] TwoBoxes =
    [
        new(new Vector2(0, 0), new Vector2(10, 10)),
        new(new Vector2(20, 0), new Vector2(30, 10)),
    ];

    [Fact]
    public void ContainsAny_FindsHitInSecondRect()
    {
        Assert.True(TwoBoxes.ContainsAny(new Vector2(25, 5)));
        Assert.False(TwoBoxes.ContainsAny(new Vector2(15, 5)));
    }

    [Fact]
    public void IntersectsAny_SingleHits()
    {
        var hit = new BoundingRect(new Vector2(5, 5), new Vector2(8, 8));
        var miss = new BoundingRect(new Vector2(11, 0), new Vector2(19, 10));
        Assert.True(TwoBoxes.IntersectsAny(hit));
        Assert.False(TwoBoxes.IntersectsAny(miss));
    }

    [Fact]
    public void IntersectsAny_BetweenCollections()
    {
        BoundingRect[] overlaps =
        [
            new(new Vector2(50, 50), new Vector2(60, 60)),
            new(new Vector2(25, 5), new Vector2(28, 8)),
        ];
        BoundingRect[] disjoint =
        [
            new(new Vector2(50, 50), new Vector2(60, 60)),
            new(new Vector2(11, 0), new Vector2(19, 10)),
        ];
        Assert.True(TwoBoxes.IntersectsAny(overlaps));
        Assert.False(TwoBoxes.IntersectsAny(disjoint));
    }

    [Fact]
    public void Union_EnclosesEveryRect()
    {
        var u = TwoBoxes.Union();
        Assert.Equal(new Vector2(0, 0), u.Min);
        Assert.Equal(new Vector2(30, 10), u.Max);
    }

    [Fact]
    public void Union_EmptySpan_ReturnsEmpty()
    {
        Assert.True(Array.Empty<BoundingRect>().Union().IsEmpty);
    }

    [Fact]
    public void IntersectsAny_EmptySpans_ReturnFalse()
    {
        var none = Array.Empty<BoundingRect>();
        Assert.False(none.IntersectsAny(TwoBoxes[0]));
        Assert.False(none.IntersectsAny(TwoBoxes));
        Assert.False(TwoBoxes.IntersectsAny(none));
    }
}

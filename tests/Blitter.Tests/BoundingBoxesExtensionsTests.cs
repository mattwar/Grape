using System.Numerics;
using Blitter.Bits;

namespace Blitter.Tests;

public class BoundingBoxesExtensionsTests
{
    private static readonly BoundingBox[] TwoBoxes =
    [
        new(new Vector3(0, 0, 0), new Vector3(10, 10, 10)),
        new(new Vector3(20, 0, 0), new Vector3(30, 10, 10)),
    ];

    [Fact]
    public void ContainsAny_FindsHitInSecondBox()
    {
        Assert.True(TwoBoxes.ContainsAny(new Vector3(25, 5, 5)));
        Assert.False(TwoBoxes.ContainsAny(new Vector3(15, 5, 5)));
    }

    [Fact]
    public void IntersectsAny_SingleHits()
    {
        var hit = new BoundingBox(new Vector3(5, 5, 5), new Vector3(8, 8, 8));
        var miss = new BoundingBox(new Vector3(11, 0, 0), new Vector3(19, 10, 10));
        Assert.True(TwoBoxes.IntersectsAny(hit));
        Assert.False(TwoBoxes.IntersectsAny(miss));
    }

    [Fact]
    public void IntersectsAny_BetweenCollections()
    {
        BoundingBox[] overlaps =
        [
            new(new Vector3(50, 50, 50), new Vector3(60, 60, 60)),
            new(new Vector3(25, 5, 5), new Vector3(28, 8, 8)),
        ];
        BoundingBox[] disjoint =
        [
            new(new Vector3(50, 50, 50), new Vector3(60, 60, 60)),
            new(new Vector3(11, 0, 0), new Vector3(19, 10, 10)),
        ];
        Assert.True(TwoBoxes.IntersectsAny(overlaps));
        Assert.False(TwoBoxes.IntersectsAny(disjoint));
    }

    [Fact]
    public void Union_EnclosesEveryBox()
    {
        var u = TwoBoxes.Union();
        Assert.Equal(new Vector3(0, 0, 0), u.Min);
        Assert.Equal(new Vector3(30, 10, 10), u.Max);
    }

    [Fact]
    public void Union_EmptySpan_ReturnsEmpty()
    {
        Assert.True(Array.Empty<BoundingBox>().Union().IsEmpty);
    }

    [Fact]
    public void IntersectsAny_EmptySpans_ReturnFalse()
    {
        var none = Array.Empty<BoundingBox>();
        Assert.False(none.IntersectsAny(TwoBoxes[0]));
        Assert.False(none.IntersectsAny(TwoBoxes));
        Assert.False(TwoBoxes.IntersectsAny(none));
    }
}

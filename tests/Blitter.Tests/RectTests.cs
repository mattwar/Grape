using System.Numerics;

namespace Blitter.Tests;

public class RectTests
{
    [Fact]
    public void Constructor_StoresFields()
    {
        var r = new Rect(1, 2, 3, 4);
        Assert.Equal(1f, r.X);
        Assert.Equal(2f, r.Y);
        Assert.Equal(3f, r.Width);
        Assert.Equal(4f, r.Height);
    }

    [Fact]
    public void Constructor_FromVector_StoresPosition()
    {
        var r = new Rect(new Vector2(5, 6), 7, 8);
        Assert.Equal(new Vector2(5, 6), r.Position);
        Assert.Equal(7f, r.Width);
        Assert.Equal(8f, r.Height);
    }

    [Fact]
    public void Edges_ComputeFromPositionAndSize()
    {
        var r = new Rect(10, 20, 30, 40);
        Assert.Equal(10f, r.Left);
        Assert.Equal(20f, r.Top);
        Assert.Equal(40f, r.Right);
        Assert.Equal(60f, r.Bottom);
    }

    [Fact]
    public void Equality_IsByValue()
    {
        Assert.Equal(new Rect(1, 2, 3, 4), new Rect(1, 2, 3, 4));
        Assert.NotEqual(new Rect(1, 2, 3, 4), new Rect(1, 2, 3, 5));
        Assert.True(new Rect(1, 2, 3, 4) == new Rect(1, 2, 3, 4));
        Assert.True(new Rect(1, 2, 3, 4) != new Rect(0, 2, 3, 4));
    }

    [Fact]
    public void GetHashCode_StableAcrossEqualValues()
    {
        Assert.Equal(new Rect(1, 2, 3, 4).GetHashCode(), new Rect(1, 2, 3, 4).GetHashCode());
    }
}

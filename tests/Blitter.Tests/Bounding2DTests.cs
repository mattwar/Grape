using System.Numerics;
using Blitter.Bits;

namespace Blitter.Tests;

public class Bounding2DTests
{
    private const float Eps = 1e-4f;

    // ---------------- BoundingRect ----------------

    [Fact]
    public void BoundingRect_Empty_IsEmpty()
    {
        Assert.True(BoundingRect.Empty.IsEmpty);
    }

    [Fact]
    public void BoundingRect_FromPoints_ComputesMinMax()
    {
        Vector2[] pts = [new(1, 2), new(-4, 5), new(0, -1)];
        var r = BoundingRect.FromPoints(pts);
        Assert.Equal(new Vector2(-4, -1), r.Min);
        Assert.Equal(new Vector2(1, 5), r.Max);
        Assert.Equal(new Vector2(-1.5f, 2f), r.Center);
        Assert.Equal(new Vector2(5, 6), r.Size);
        Assert.False(r.IsEmpty);
    }

    [Fact]
    public void BoundingRect_FromPoints_EmptySpan_ReturnsEmpty()
    {
        Assert.True(BoundingRect.FromPoints(ReadOnlySpan<Vector2>.Empty).IsEmpty);
    }

    [Fact]
    public void BoundingRect_FromCenterSize_RoundTrip()
    {
        var r = BoundingRect.FromCenterSize(new Vector2(10, 20), new Vector2(2, 4));
        Assert.Equal(new Vector2(9, 18), r.Min);
        Assert.Equal(new Vector2(11, 22), r.Max);
        Assert.Equal(new Vector2(10, 20), r.Center);
    }

    [Fact]
    public void BoundingRect_FromVertices_OnVertex2D()
    {
        Vertex2D[] verts =
        [
            new(new Vector2(-1, -2)),
            new(new Vector2( 4,  5)),
        ];
        var r = BoundingRect.FromVertices<Vertex2D>(verts);
        Assert.Equal(new Vector2(-1, -2), r.Min);
        Assert.Equal(new Vector2(4, 5), r.Max);
    }

    [Fact]
    public void BoundingRect_RectInterop_RoundTrip()
    {
        var src = new Rect(10, 20, 30, 40);
        var br = BoundingRect.FromRect(src);
        Assert.Equal(new Vector2(10, 20), br.Min);
        Assert.Equal(new Vector2(40, 60), br.Max);
        var back = br.ToRect();
        Assert.Equal(src, back);
    }

    [Fact]
    public void BoundingRect_Encapsulate_ExpandsToFitPoint()
    {
        var b = new BoundingRect(Vector2.Zero, Vector2.One);
        var grown = b.Encapsulate(new Vector2(5, -2));
        Assert.Equal(new Vector2(0, -2), grown.Min);
        Assert.Equal(new Vector2(5, 1), grown.Max);
    }

    [Fact]
    public void BoundingRect_Encapsulate_OnEmpty_GivesPointRect()
    {
        var grown = BoundingRect.Empty.Encapsulate(new Vector2(7, 8));
        Assert.False(grown.IsEmpty);
        Assert.Equal(new Vector2(7, 8), grown.Min);
        Assert.Equal(new Vector2(7, 8), grown.Max);
    }

    [Fact]
    public void BoundingRect_ContainsAndIntersects()
    {
        var b = new BoundingRect(Vector2.Zero, new Vector2(2, 2));
        Assert.True(b.Contains(new Vector2(1, 1)));
        Assert.False(b.Contains(new Vector2(3, 1)));

        var inside = new BoundingRect(new Vector2(0.5f), new Vector2(1.5f));
        var overlapping = new BoundingRect(new Vector2(1f), new Vector2(3f));
        var disjoint = new BoundingRect(new Vector2(5f), new Vector2(6f));

        Assert.True(b.Contains(inside));
        Assert.False(b.Contains(overlapping));
        Assert.True(b.Intersects(overlapping));
        Assert.False(b.Intersects(disjoint));
    }

    [Fact]
    public void BoundingRect_Transform_RotatedSquareStillBounds()
    {
        var b = new BoundingRect(new Vector2(-1, -1), new Vector2(1, 1));
        var t = b.Transform(Matrix3x2.CreateRotation(MathF.PI * 0.25f));
        Assert.Equal(MathF.Sqrt(2f), t.Max.X, Eps);
        Assert.Equal(MathF.Sqrt(2f), t.Max.Y, Eps);
    }

    // ---------------- BoundingCircle ----------------

    [Fact]
    public void BoundingCircle_Empty_IsEmpty()
    {
        Assert.True(BoundingCircle.Empty.IsEmpty);
    }

    [Fact]
    public void BoundingCircle_FromPoints_CentroidPlusFarthest()
    {
        Vector2[] pts = [new(-1, 0), new(1, 0), new(0, 1), new(0, -1)];
        var c = BoundingCircle.FromPoints(pts);
        Assert.Equal(Vector2.Zero, c.Center);
        Assert.Equal(1f, c.Radius, Eps);
    }

    [Fact]
    public void BoundingCircle_FromRect_TouchesCorners()
    {
        var r = new BoundingRect(new Vector2(-1, -1), new Vector2(1, 1));
        var c = BoundingCircle.FromRect(r);
        Assert.Equal(Vector2.Zero, c.Center);
        Assert.Equal(MathF.Sqrt(2f), c.Radius, Eps);
    }

    [Fact]
    public void BoundingCircle_Intersects_CircleAndRect()
    {
        var c = new BoundingCircle(Vector2.Zero, 1f);
        Assert.True(c.Intersects(new BoundingCircle(new Vector2(1.5f, 0), 1f)));
        Assert.False(c.Intersects(new BoundingCircle(new Vector2(3f, 0), 1f)));

        var rect = new BoundingRect(new Vector2(0.5f, -0.5f), new Vector2(2f, 0.5f));
        Assert.True(c.Intersects(rect));
        Assert.False(c.Intersects(new BoundingRect(new Vector2(5f), new Vector2(6f))));
    }

    [Fact]
    public void BoundingCircle_Encapsulate_GrowsMinimally()
    {
        var s = new BoundingCircle(Vector2.Zero, 1f);
        var g = s.Encapsulate(new Vector2(3, 0));
        Assert.Equal(new Vector2(1, 0), g.Center);
        Assert.Equal(2f, g.Radius, Eps);

        var unchanged = s.Encapsulate(new Vector2(0.5f, 0));
        Assert.Equal(s, unchanged);
    }

    [Fact]
    public void BoundingCircle_Transform_TranslateAndScale()
    {
        var c = new BoundingCircle(new Vector2(1, 0), 2f);
        var t = c.Transform(Matrix3x2.CreateScale(3f) * Matrix3x2.CreateTranslation(new Vector2(0, 5)));
        Assert.Equal(new Vector2(3, 5), t.Center);
        Assert.Equal(6f, t.Radius, Eps);
    }
}

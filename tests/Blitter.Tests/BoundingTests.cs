using System.Numerics;
using Blitter.Bits;

namespace Blitter.Tests;

public class BoundingTests
{
    private const float Eps = 1e-4f;

    // ---------------- BoundingBox ----------------

    [Fact]
    public void BoundingBox_Empty_IsEmpty()
    {
        var b = BoundingBox.Empty;
        Assert.True(b.IsEmpty);
    }

    [Fact]
    public void BoundingBox_FromPoints_ComputesMinMax()
    {
        Vector3[] pts = [new(1, 2, 3), new(-4, 5, 0), new(0, -1, 6)];
        var b = BoundingBox.FromPoints(pts);
        Assert.Equal(new Vector3(-4, -1, 0), b.Min);
        Assert.Equal(new Vector3(1, 5, 6), b.Max);
        Assert.Equal(new Vector3(-1.5f, 2f, 3f), b.Center);
        Assert.Equal(new Vector3(5, 6, 6), b.Size);
        Assert.False(b.IsEmpty);
    }

    [Fact]
    public void BoundingBox_FromPoints_EmptySpan_ReturnsEmpty()
    {
        Assert.True(BoundingBox.FromPoints(ReadOnlySpan<Vector3>.Empty).IsEmpty);
    }

    [Fact]
    public void BoundingBox_FromCenterSize_RoundTrip()
    {
        var b = BoundingBox.FromCenterSize(new Vector3(10, 20, 30), new Vector3(2, 4, 6));
        Assert.Equal(new Vector3(9, 18, 27), b.Min);
        Assert.Equal(new Vector3(11, 22, 33), b.Max);
        Assert.Equal(new Vector3(10, 20, 30), b.Center);
    }

    [Fact]
    public void BoundingBox_FromVertices_WorksOnLitVertex()
    {
        LitVertex3D[] verts =
        [
            new(new Vector3(-1, -2, -3), Vector3.UnitY, Color.White),
            new(new Vector3( 4,  5,  6), Vector3.UnitY, Color.White),
        ];
        var b = BoundingBox.FromVertices<LitVertex3D>(verts);
        Assert.Equal(new Vector3(-1, -2, -3), b.Min);
        Assert.Equal(new Vector3(4, 5, 6), b.Max);
    }

    [Fact]
    public void BoundingBox_Encapsulate_ExpandsToFitPoint()
    {
        var b = new BoundingBox(Vector3.Zero, Vector3.One);
        var grown = b.Encapsulate(new Vector3(5, -2, 0.5f));
        Assert.Equal(new Vector3(0, -2, 0), grown.Min);
        Assert.Equal(new Vector3(5, 1, 1), grown.Max);
    }

    [Fact]
    public void BoundingBox_Encapsulate_OnEmpty_GivesPointBox()
    {
        var grown = BoundingBox.Empty.Encapsulate(new Vector3(7, 8, 9));
        Assert.False(grown.IsEmpty);
        Assert.Equal(new Vector3(7, 8, 9), grown.Min);
        Assert.Equal(new Vector3(7, 8, 9), grown.Max);
    }

    [Fact]
    public void BoundingBox_ContainsAndIntersects()
    {
        var b = new BoundingBox(Vector3.Zero, new Vector3(2, 2, 2));
        Assert.True(b.Contains(new Vector3(1, 1, 1)));
        Assert.False(b.Contains(new Vector3(3, 1, 1)));

        var inside = new BoundingBox(new Vector3(0.5f), new Vector3(1.5f));
        var overlapping = new BoundingBox(new Vector3(1f), new Vector3(3f));
        var disjoint = new BoundingBox(new Vector3(5f), new Vector3(6f));

        Assert.True(b.Contains(inside));
        Assert.False(b.Contains(overlapping));
        Assert.True(b.Intersects(overlapping));
        Assert.False(b.Intersects(disjoint));
    }

    [Fact]
    public void BoundingBox_Transform_RotatedCubeStillBounds()
    {
        var b = new BoundingBox(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));
        // 45deg around Y -- new AABB should expand to ~sqrt(2) on X/Z.
        var t = b.Transform(Matrix4x4.CreateRotationY(MathF.PI * 0.25f));
        Assert.Equal(MathF.Sqrt(2f), t.Max.X, Eps);
        Assert.Equal(MathF.Sqrt(2f), t.Max.Z, Eps);
        Assert.Equal(1f, t.Max.Y, Eps);
    }

    // ---------------- BoundingSphere ----------------

    [Fact]
    public void BoundingSphere_Empty_IsEmpty()
    {
        Assert.True(BoundingSphere.Empty.IsEmpty);
    }

    [Fact]
    public void BoundingSphere_FromPoints_CentroidPlusFarthest()
    {
        Vector3[] pts = [new(-1, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, -1, 0)];
        var s = BoundingSphere.FromPoints(pts);
        Assert.Equal(Vector3.Zero, s.Center);
        Assert.Equal(1f, s.Radius, Eps);
    }

    [Fact]
    public void BoundingSphere_FromBox_TouchesCorners()
    {
        var b = new BoundingBox(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));
        var s = BoundingSphere.FromBox(b);
        Assert.Equal(Vector3.Zero, s.Center);
        Assert.Equal(MathF.Sqrt(3f), s.Radius, Eps);
    }

    [Fact]
    public void BoundingSphere_Intersects_SphereAndBox()
    {
        var s = new BoundingSphere(Vector3.Zero, 1f);
        Assert.True(s.Intersects(new BoundingSphere(new Vector3(1.5f, 0, 0), 1f)));
        Assert.False(s.Intersects(new BoundingSphere(new Vector3(3f, 0, 0), 1f)));

        var box = new BoundingBox(new Vector3(0.5f, -0.5f, -0.5f), new Vector3(2f, 0.5f, 0.5f));
        Assert.True(s.Intersects(box));
        Assert.False(s.Intersects(new BoundingBox(new Vector3(5f), new Vector3(6f))));
    }

    [Fact]
    public void BoundingSphere_Encapsulate_GrowsMinimally()
    {
        var s = new BoundingSphere(Vector3.Zero, 1f);
        var g = s.Encapsulate(new Vector3(3, 0, 0));
        // New sphere should pass through (-1,0,0) and (3,0,0) -> center=(1,0,0), r=2.
        Assert.Equal(new Vector3(1, 0, 0), g.Center);
        Assert.Equal(2f, g.Radius, Eps);

        // Inside point: no growth.
        var unchanged = s.Encapsulate(new Vector3(0.5f, 0, 0));
        Assert.Equal(s, unchanged);
    }

    [Fact]
    public void BoundingSphere_Transform_TranslateAndScale()
    {
        var s = new BoundingSphere(new Vector3(1, 0, 0), 2f);
        var t = s.Transform(Matrix4x4.CreateScale(3f) * Matrix4x4.CreateTranslation(new Vector3(0, 5, 0)));
        Assert.Equal(new Vector3(3, 5, 0), t.Center);
        Assert.Equal(6f, t.Radius, Eps);
    }

    // ---------------- Mesh extensions ----------------

    [Fact]
    public void Mesh_ComputeBoundingBox_OnUnitCube()
    {
        var cube = Meshes.Cube(Color.White, size: new Vector3(2f));
        var b = cube.ComputeBoundingBox();
        Assert.Equal(new Vector3(-1, -1, -1), b.Min);
        Assert.Equal(new Vector3(1, 1, 1), b.Max);
    }

    [Fact]
    public void Mesh_ComputeBoundingSphere_OnUnitCube()
    {
        var cube = Meshes.Cube(Color.White, size: new Vector3(2f));
        var s = cube.ComputeBoundingSphere();
        Assert.Equal(Vector3.Zero, s.Center);
        Assert.Equal(MathF.Sqrt(3f), s.Radius, Eps);
    }

    [Fact]
    public void Mesh_ComputeCenter_OnTranslatedCube()
    {
        var cube = Meshes.Cube(Color.White, size: new Vector3(2f)).Transform(Matrix4x4.CreateTranslation(10, 20, 30));
        Assert.Equal(new Vector3(10, 20, 30), cube.ComputeCenter());
    }
}

using System.Numerics;
using System.Runtime.InteropServices;

using Blitter.Bits;

namespace Blitter.Tests;

public class MeshesTests
{
    private const float Epsilon = 1e-4f;

    // ---------------- helpers ----------------

    private static (Vector3 Min, Vector3 Max) BoundsOfPositions(ReadOnlySpan<Vector3> positions)
    {
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        foreach (var p in positions)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return (min, max);
    }

    private static Vector3[] PositionsOfLit(Mesh<LitVertex3D> mesh)
    {
        var span = mesh.Vertices;
        var p = new Vector3[span.Length];
        for (int i = 0; i < span.Length; i++) p[i] = span[i].Position;
        return p;
    }

    private static Vector3[] NormalsOfLit(Mesh<LitVertex3D> mesh)
    {
        var span = mesh.Vertices;
        var n = new Vector3[span.Length];
        for (int i = 0; i < span.Length; i++) n[i] = span[i].Normal;
        return n;
    }

    private static Vector3[] PositionsOfColor(Mesh<ColorVertex3D> mesh)
    {
        var span = mesh.Vertices;
        var p = new Vector3[span.Length];
        for (int i = 0; i < span.Length; i++) p[i] = span[i].Position;
        return p;
    }

    private static void AssertCenteredOnOrigin(ReadOnlySpan<Vector3> positions, float halfExtent, float epsilon = Epsilon)
    {
        var (min, max) = BoundsOfPositions(positions);
        Assert.Equal(-halfExtent, min.X, epsilon);
        Assert.Equal(-halfExtent, min.Y, epsilon);
        Assert.Equal(-halfExtent, min.Z, epsilon);
        Assert.Equal(halfExtent, max.X, epsilon);
        Assert.Equal(halfExtent, max.Y, epsilon);
        Assert.Equal(halfExtent, max.Z, epsilon);
    }

    // ---------------- Rectangle / TexturedRectangle / Circle / Ring ----------------

    [Fact]
    public void Rectangle_HasFourVerticesSixIndices_AndUnitExtent()
    {
        var mesh = Meshes.Rectangle(new Color(1, 2, 3));
        Assert.Equal(4, mesh.VertexCount);
        Assert.Equal(6, mesh.IndexCount);
        var positions = PositionsOfColor(mesh);
        var (min, max) = BoundsOfPositions(positions);
        Assert.Equal(-0.5f, min.X, Epsilon);
        Assert.Equal( 0.5f, max.X, Epsilon);
        Assert.Equal(-0.5f, min.Y, Epsilon);
        Assert.Equal( 0.5f, max.Y, Epsilon);
        Assert.Equal( 0f,   min.Z, Epsilon);
        Assert.Equal( 0f,   max.Z, Epsilon);
    }

    [Fact]
    public void TexturedRectangle_UvsCoverFullRangeWithTopLeftOrigin()
    {
        var mesh = Meshes.TexturedRectangle();
        var span = mesh.Vertices;
        // The vertex at the top-left of the quad (-0.5, +0.5) should have UV (0,0).
        foreach (var v in span)
        {
            if (Math.Abs(v.Position.X - -0.5f) < Epsilon && Math.Abs(v.Position.Y - 0.5f) < Epsilon)
            {
                Assert.Equal(0f, v.TextureCoordinate.X, Epsilon);
                Assert.Equal(0f, v.TextureCoordinate.Y, Epsilon);
                return;
            }
        }
        Assert.Fail("Top-left vertex not found");
    }

    [Fact]
    public void Circle_VertexCountMatchesSegmentsPlusCenter()
    {
        var mesh = Meshes.Circle(Color.White, radius: 1f, segments: 16);
        Assert.Equal(17, mesh.VertexCount);
        Assert.Equal(48, mesh.IndexCount);
    }

    [Fact]
    public void Ellipse_RimMatchesNonUniformSize()
    {
        var mesh = Meshes.Ellipse(Color.White, size: new Vector2(4f, 2f), segments: 16);
        Assert.Equal(17, mesh.VertexCount);
        var positions = PositionsOfColor(mesh);
        var (min, max) = BoundsOfPositions(positions);
        Assert.Equal(-2f, min.X, Epsilon);
        Assert.Equal(-1f, min.Y, Epsilon);
        Assert.Equal( 2f, max.X, Epsilon);
        Assert.Equal( 1f, max.Y, Epsilon);
    }

    [Fact]
    public void Ring_VertexCountMatchesTwoPerSegment()
    {
        var mesh = Meshes.Ring(Color.White, innerRadius: 0.5f, outerRadius: 1f, segments: 24);
        Assert.Equal(48, mesh.VertexCount);
        Assert.Equal(24 * 6, mesh.IndexCount);
    }

    // ---------------- Plane ----------------

    [Fact]
    public void Plane_DefaultIsSingleQuadOnXz()
    {
        var mesh = Meshes.Plane(Color.White);
        Assert.Equal(4, mesh.VertexCount);
        Assert.Equal(6, mesh.IndexCount);

        var positions = PositionsOfLit(mesh);
        foreach (var p in positions)
            Assert.Equal(0f, p.Y, Epsilon);

        // All normals should be +Y.
        foreach (var n in NormalsOfLit(mesh))
        {
            Assert.Equal(0f, n.X, Epsilon);
            Assert.Equal(1f, n.Y, Epsilon);
            Assert.Equal(0f, n.Z, Epsilon);
        }
    }

    [Fact]
    public void Plane_SubdividedHasExpectedVertexAndTriangleCount()
    {
        var mesh = Meshes.Plane(Color.White, size: new Vector2(2f), subdivisions: 4);
        // (subdivisions+1)^2 vertices, subdivisions^2 quads * 6 indices.
        Assert.Equal(25, mesh.VertexCount);
        Assert.Equal(16 * 6, mesh.IndexCount);
    }

    // ---------------- Cube ----------------

    [Fact]
    public void Cube_Has24VerticesAnd36Indices_AndIsUnitExtent()
    {
        var mesh = Meshes.Cube(Color.White);
        Assert.Equal(24, mesh.VertexCount);
        Assert.Equal(36, mesh.IndexCount);
        AssertCenteredOnOrigin(PositionsOfLit(mesh), 0.5f);
    }

    [Fact]
    public void Cube_NormalsAreUnitLengthAndPointOutward()
    {
        var mesh = Meshes.Cube(Color.White);
        var span = mesh.Vertices;
        foreach (var v in span)
        {
            Assert.Equal(1f, v.Normal.Length(), Epsilon);
            // For an origin-centered convex shape, the outward normal at any
            // surface vertex should have a positive dot product with the
            // vertex position (which points from center to surface).
            Assert.True(Vector3.Dot(v.Normal, v.Position) > 0f, "Normal should face outward");
        }
    }

    [Fact]
    public void Cube_FaceWindingMatchesStoredNormal()
    {
        var mesh = Meshes.Cube(Color.White);
        var verts = mesh.Vertices;
        var idx = mesh.Indices;
        for (int i = 0; i < idx.Length; i += 3)
        {
            var a = verts[(int)idx[i]];
            var b = verts[(int)idx[i + 1]];
            var c = verts[(int)idx[i + 2]];
            var faceNormal = Vector3.Normalize(Vector3.Cross(b.Position - a.Position, c.Position - a.Position));
            Assert.True(Vector3.Dot(faceNormal, a.Normal) > 0.99f,
                $"Triangle winding does not match stored normal for face starting at index {i}");
        }
    }

    [Fact]
    public void Cube_NonCubicSizeRespectedPerAxis()
    {
        var mesh = Meshes.Cube(Color.White, size: new Vector3(2f, 4f, 6f));
        var (min, max) = BoundsOfPositions(PositionsOfLit(mesh));
        Assert.Equal(-1f, min.X, Epsilon); Assert.Equal(1f, max.X, Epsilon);
        Assert.Equal(-2f, min.Y, Epsilon); Assert.Equal(2f, max.Y, Epsilon);
        Assert.Equal(-3f, min.Z, Epsilon); Assert.Equal(3f, max.Z, Epsilon);
    }

    [Fact]
    public void TexturedCube_Has24VerticesAnd36Indices()
    {
        var mesh = Meshes.TexturedCube();
        Assert.Equal(24, mesh.VertexCount);
        Assert.Equal(36, mesh.IndexCount);
    }

    // ---------------- Sphere / Icosphere ----------------

    [Fact]
    public void Sphere_AllVerticesLieOnRadius_NormalsArePositionDirection()
    {
        const float r = 0.7f;
        var mesh = Meshes.Sphere(Color.White, radius: r, latitudeSegments: 8, longitudeSegments: 12);
        var span = mesh.Vertices;
        foreach (var v in span)
        {
            Assert.Equal(r, v.Position.Length(), 1e-3f);
            Assert.Equal(1f, v.Normal.Length(), 1e-3f);
            // Normal should match Normalize(position).
            var expected = Vector3.Normalize(v.Position);
            Assert.True(Vector3.Distance(expected, v.Normal) < 1e-3f);
        }
    }

    [Fact]
    public void Icosphere_ZeroSubdivisionsIsIcosahedron()
    {
        var mesh = Meshes.Icosphere(Color.White, radius: 1f, subdivisions: 0);
        Assert.Equal(12, mesh.VertexCount);
        Assert.Equal(60, mesh.IndexCount); // 20 faces * 3
        var span = mesh.Vertices;
        foreach (var v in span)
            Assert.Equal(1f, v.Position.Length(), 1e-4f);
    }

    [Fact]
    public void Icosphere_SubdivisionMultipliesFaceCountByFour()
    {
        var s0 = Meshes.Icosphere(Color.White, subdivisions: 0);
        var s1 = Meshes.Icosphere(Color.White, subdivisions: 1);
        Assert.Equal(s0.IndexCount * 4, s1.IndexCount);
    }

    // ---------------- Cylinder / Cone / Capsule / Torus ----------------

    [Fact]
    public void Cylinder_CappedHasMoreVerticesThanUncapped()
    {
        var capped = Meshes.Cylinder(Color.White, segments: 8, capped: true);
        var open   = Meshes.Cylinder(Color.White, segments: 8, capped: false);
        Assert.True(capped.VertexCount > open.VertexCount);
        Assert.True(capped.IndexCount  > open.IndexCount);
    }

    [Fact]
    public void Cylinder_BoundsMatchRadiusAndHeight()
    {
        var mesh = Meshes.Cylinder(Color.White, radius: 0.5f, height: 2f, segments: 12);
        var (min, max) = BoundsOfPositions(PositionsOfLit(mesh));
        Assert.Equal(-1f, min.Y, Epsilon);
        Assert.Equal( 1f, max.Y, Epsilon);
        Assert.Equal(-0.5f, min.X, 1e-3f);
        Assert.Equal( 0.5f, max.X, 1e-3f);
    }

    [Fact]
    public void Cone_ApexIsAtPlusHalfHeight()
    {
        var mesh = Meshes.Cone(Color.White, radius: 0.5f, height: 2f, segments: 12);
        var positions = PositionsOfLit(mesh);
        var (_, max) = BoundsOfPositions(positions);
        Assert.Equal(1f, max.Y, Epsilon);
    }

    [Fact]
    public void Capsule_TotalHeightIncludesBothHemispheres()
    {
        const float r = 0.25f, h = 1f;
        var mesh = Meshes.Capsule(Color.White, radius: r, height: h, segments: 12, hemisphereRings: 4);
        var (min, max) = BoundsOfPositions(PositionsOfLit(mesh));
        Assert.Equal(-(h * 0.5f + r), min.Y, 1e-3f);
        Assert.Equal( (h * 0.5f + r), max.Y, 1e-3f);
        Assert.Equal(-r, min.X, 1e-3f);
        Assert.Equal( r, max.X, 1e-3f);
    }

    [Fact]
    public void Torus_OuterAndInnerRadiiMatch()
    {
        const float major = 0.5f, minor = 0.1f;
        var mesh = Meshes.Torus(Color.White, majorRadius: major, minorRadius: minor,
            majorSegments: 24, minorSegments: 12);
        var positions = PositionsOfLit(mesh);
        var (min, max) = BoundsOfPositions(positions);
        Assert.Equal(major + minor, max.X, 1e-3f);
        Assert.Equal(-(major + minor), min.X, 1e-3f);
        Assert.Equal( minor, max.Y, 1e-3f);
        Assert.Equal(-minor, min.Y, 1e-3f);
    }

    // ---------------- Platonic solids ----------------

    [Fact]
    public void Tetrahedron_HasFourFaces_VerticesOnRadius()
    {
        var mesh = Meshes.Tetrahedron(Color.White, radius: 1f);
        Assert.Equal(12, mesh.VertexCount); // flat-shaded: 3 verts per face, 4 faces
        var span = mesh.Vertices;
        foreach (var v in span)
            Assert.Equal(1f, v.Position.Length(), 1e-3f);
    }

    [Fact]
    public void Octahedron_HasEightFaces()
    {
        var mesh = Meshes.Octahedron(Color.White);
        Assert.Equal(24, mesh.VertexCount); // 8 faces * 3
    }

    [Fact]
    public void Icosahedron_HasTwentyFaces()
    {
        var mesh = Meshes.Icosahedron(Color.White);
        Assert.Equal(60, mesh.VertexCount); // 20 faces * 3 (flat-shaded)
    }

    // ---------------- Lines ----------------

    [Fact]
    public void Axes_IsLineListWithSixVertices()
    {
        var mesh = Meshes.Axes(length: 2f);
        Assert.Equal(Topology.LineList, mesh.Topology);
        Assert.Equal(6, mesh.VertexCount);
    }

    [Fact]
    public void Grid_LineCountMatchesCellsPerSide()
    {
        var mesh = Meshes.Grid(cellsPerSide: 4);
        Assert.Equal(Topology.LineList, mesh.Topology);
        // (cellsPerSide+1) lines per axis * 2 axes * 2 endpoints = 5*2*2 = 20.
        Assert.Equal(20, mesh.VertexCount);
    }

    [Fact]
    public void WireBox_HasTwentyFourVerticesForTwelveEdges()
    {
        var mesh = Meshes.WireBox(Color.White, size: new Vector3(2f, 2f, 2f));
        Assert.Equal(Topology.LineList, mesh.Topology);
        Assert.Equal(24, mesh.VertexCount);
        AssertCenteredOnOrigin(PositionsOfColor(mesh), 1f);
    }
}

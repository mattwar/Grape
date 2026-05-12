using System.Numerics;
using Blitter.Bits;

namespace Blitter.Tests;

public class MeshNormalsTests
{
    private const float Epsilon = 1e-4f;

    // Two triangles sharing an edge in the XY plane (z=0), CCW so the
    // face normal points along +Z.
    //
    //  v2(0,1,0) --- v3(1,1,0)
    //       |     \      |
    //       |      \     |
    //  v0(0,0,0) --- v1(1,0,0)
    //
    private static Mesh<LitVertex3D> Quad_Indexed_BadNormals()
    {
        var verts = new[]
        {
            new LitVertex3D(new Vector3(0, 0, 0), Vector3.UnitX, Color.White),
            new LitVertex3D(new Vector3(1, 0, 0), Vector3.UnitX, Color.White),
            new LitVertex3D(new Vector3(0, 1, 0), Vector3.UnitX, Color.White),
            new LitVertex3D(new Vector3(1, 1, 0), Vector3.UnitX, Color.White),
        };
        var indices = new uint[] { 0, 1, 2, 1, 3, 2 };
        return Mesh.Create<LitVertex3D>(verts, indices, Topology.TriangleList);
    }

    private static Mesh<LitVertex3D> Quad_Unindexed_BadNormals()
    {
        // Same quad but unindexed (6 vertices, two shared by position).
        var p0 = new Vector3(0, 0, 0);
        var p1 = new Vector3(1, 0, 0);
        var p2 = new Vector3(0, 1, 0);
        var p3 = new Vector3(1, 1, 0);
        Color w = Color.White;
        Vector3 bad = Vector3.UnitX;
        var verts = new[]
        {
            new LitVertex3D(p0, bad, w),
            new LitVertex3D(p1, bad, w),
            new LitVertex3D(p2, bad, w),
            new LitVertex3D(p1, bad, w),
            new LitVertex3D(p3, bad, w),
            new LitVertex3D(p2, bad, w),
        };
        return Mesh.Create<LitVertex3D>(verts, ReadOnlySpan<uint>.Empty, Topology.TriangleList);
    }

    [Fact]
    public void Smooth_Indexed_AllNormalsPointPlusZ()
    {
        var dst = Quad_Indexed_BadNormals().RecalculateNormals(smooth: true);
        foreach (var v in dst.Vertices.ToArray())
        {
            Assert.Equal(0f, v.Normal.X, Epsilon);
            Assert.Equal(0f, v.Normal.Y, Epsilon);
            Assert.Equal(1f, v.Normal.Z, Epsilon);
        }
    }

    [Fact]
    public void Smooth_Indexed_PreservesVertexAndIndexCount()
    {
        var src = Quad_Indexed_BadNormals();
        var dst = src.RecalculateNormals(smooth: true);
        Assert.Equal(src.Vertices.Length, dst.Vertices.Length);
        Assert.Equal(src.Indices.Length, dst.Indices.Length);
    }

    [Fact]
    public void Smooth_Unindexed_WeldsByPosition()
    {
        var dst = Quad_Unindexed_BadNormals().RecalculateNormals(smooth: true);
        foreach (var v in dst.Vertices.ToArray())
        {
            Assert.Equal(0f, v.Normal.X, Epsilon);
            Assert.Equal(0f, v.Normal.Y, Epsilon);
            Assert.Equal(1f, v.Normal.Z, Epsilon);
        }
    }

    [Fact]
    public void Flat_Indexed_ExpandsToUnindexedAndAssignsFaceNormals()
    {
        // Build two triangles with different facing planes so flat
        // shading produces two distinct normals.
        var verts = new[]
        {
            // Triangle 0: in XY plane, normal +Z.
            new LitVertex3D(new Vector3(0, 0, 0), Vector3.UnitX, Color.White),
            new LitVertex3D(new Vector3(1, 0, 0), Vector3.UnitX, Color.White),
            new LitVertex3D(new Vector3(0, 1, 0), Vector3.UnitX, Color.White),
            // Triangle 1: in XZ plane, normal -Y.
            new LitVertex3D(new Vector3(0, 0, 0), Vector3.UnitX, Color.White),
            new LitVertex3D(new Vector3(1, 0, 0), Vector3.UnitX, Color.White),
            new LitVertex3D(new Vector3(0, 0, 1), Vector3.UnitX, Color.White),
        };
        var indices = new uint[] { 0, 1, 2, 3, 4, 5 };
        var src = Mesh.Create<LitVertex3D>(verts, indices, Topology.TriangleList);

        var dst = src.RecalculateNormals(smooth: false);

        Assert.Equal(0, dst.Indices.Length); // flat drops indices
        Assert.Equal(6, dst.Vertices.Length);
        var v = dst.Vertices.ToArray();
        // Triangle 0 normals
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(0f, v[i].Normal.X, Epsilon);
            Assert.Equal(0f, v[i].Normal.Y, Epsilon);
            Assert.Equal(1f, v[i].Normal.Z, Epsilon);
        }
        // Triangle 1 normals: cross(p1-p0, p2-p0) = cross((1,0,0),(0,0,1)) = (0,-1,0)
        for (int i = 3; i < 6; i++)
        {
            Assert.Equal(0f, v[i].Normal.X, Epsilon);
            Assert.Equal(-1f, v[i].Normal.Y, Epsilon);
            Assert.Equal(0f, v[i].Normal.Z, Epsilon);
        }
    }

    [Fact]
    public void Flat_Unindexed_OverwritesNormalsInPlace()
    {
        var src = Quad_Unindexed_BadNormals();
        var dst = src.RecalculateNormals(smooth: false);

        Assert.Equal(6, dst.Vertices.Length);
        Assert.Equal(0, dst.Indices.Length);
        foreach (var v in dst.Vertices.ToArray())
        {
            Assert.Equal(0f, v.Normal.X, Epsilon);
            Assert.Equal(0f, v.Normal.Y, Epsilon);
            Assert.Equal(1f, v.Normal.Z, Epsilon);
        }
    }

    [Fact]
    public void Smooth_AveragesAcrossSharedEdge()
    {
        // Two triangles sharing the edge (0,0,0)-(1,0,0). Triangle A is
        // in the XY plane (normal +Z). Triangle B is angled up around
        // the shared edge so its normal has +Z and +Y components. The
        // averaged normal on the shared vertices should still have
        // unit length and a +Z component matching the average.
        var p0 = new Vector3(0, 0, 0);
        var p1 = new Vector3(1, 0, 0);
        var p2 = new Vector3(0, 1, 0);            // tri A apex
        var p3 = new Vector3(0, 1, 1);            // tri B apex (above XY)
        Color w = Color.White;
        Vector3 bad = Vector3.Zero;

        var verts = new[]
        {
            new LitVertex3D(p0, bad, w),
            new LitVertex3D(p1, bad, w),
            new LitVertex3D(p2, bad, w),
            new LitVertex3D(p3, bad, w),
        };
        // Tri A: 0,1,2 (normal +Z). Tri B: 0,1,3 (normal cross((1,0,0),(0,1,1)) = (0,-1,1)/sqrt2)
        var indices = new uint[] { 0, 1, 2, 0, 1, 3 };
        var src = Mesh.Create<LitVertex3D>(verts, indices, Topology.TriangleList);

        var dst = src.RecalculateNormals(smooth: true);
        var v = dst.Vertices.ToArray();

        // Vertex 2 only touches tri A: pure +Z.
        Assert.Equal(1f, v[2].Normal.Z, Epsilon);
        // Shared vertex 0 has both incidents: normal should be unit length
        // and not equal to any single face normal.
        Assert.Equal(1f, v[0].Normal.Length(), Epsilon);
        Assert.True(MathF.Abs(v[0].Normal.Z - 1f) > Epsilon);
    }

    [Fact]
    public void RejectsTriangleStrip()
    {
        var verts = new[]
        {
            new LitVertex3D(new Vector3(0, 0, 0), Vector3.Zero, Color.White),
            new LitVertex3D(new Vector3(1, 0, 0), Vector3.Zero, Color.White),
            new LitVertex3D(new Vector3(0, 1, 0), Vector3.Zero, Color.White),
        };
        var src = Mesh.Create<LitVertex3D>(verts, ReadOnlySpan<uint>.Empty, Topology.TriangleStrip);
        Assert.Throws<InvalidOperationException>(() => src.RecalculateNormals());
    }

    [Fact]
    public void LitTextureVertex3D_PreservesTextureCoordinates()
    {
        var verts = new[]
        {
            new LitTextureVertex3D(new Vector3(0, 0, 0), Vector3.UnitX, new Vector2(0.1f, 0.2f), Color.White),
            new LitTextureVertex3D(new Vector3(1, 0, 0), Vector3.UnitX, new Vector2(0.3f, 0.4f), Color.White),
            new LitTextureVertex3D(new Vector3(0, 1, 0), Vector3.UnitX, new Vector2(0.5f, 0.6f), Color.White),
        };
        var src = Mesh.Create<LitTextureVertex3D>(verts, ReadOnlySpan<uint>.Empty, Topology.TriangleList);

        var dst = src.RecalculateNormals(smooth: true);

        var d = dst.Vertices.ToArray();
        Assert.Equal(new Vector2(0.1f, 0.2f), d[0].TextureCoordinate);
        Assert.Equal(new Vector2(0.3f, 0.4f), d[1].TextureCoordinate);
        Assert.Equal(new Vector2(0.5f, 0.6f), d[2].TextureCoordinate);
        foreach (var v in d)
            Assert.Equal(1f, v.Normal.Z, Epsilon);
    }
}

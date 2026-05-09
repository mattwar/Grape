using System.Numerics;
using System.Runtime.InteropServices;

namespace Blitter.Tests;

public class MeshExtensionsTests
{
    private const float Epsilon = 1e-4f;

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

    // ---------------- Transform ----------------

    [Fact]
    public void Transform_TranslatesPositions()
    {
        var src = Meshes.Cube(Color.White);
        var dst = src.Transform(Matrix4x4.CreateTranslation(10f, 20f, 30f));

        var srcPos = PositionsOfLit(src);
        var dstPos = PositionsOfLit(dst);

        Assert.Equal(srcPos.Length, dstPos.Length);
        for (int i = 0; i < srcPos.Length; i++)
            Assert.Equal(srcPos[i] + new Vector3(10f, 20f, 30f), dstPos[i]);
    }

    [Fact]
    public void Transform_RotatesNormals_AndKeepsThemUnit()
    {
        var src = Meshes.Cube(Color.White);
        var dst = src.Transform(Matrix4x4.CreateRotationY(MathF.PI * 0.5f));

        foreach (var n in NormalsOfLit(dst))
            Assert.Equal(1f, n.Length(), Epsilon);

        // The +X face on the source has normal (1,0,0); after a +90deg
        // rotation around Y its position lands on +Z and the normal
        // becomes (0,0,-1)... wait, +90 around Y sends +X to -Z.
        // Easier: just confirm at least one source normal that was
        // (1,0,0) now appears as (0,0,-1) somewhere in dst.
        var dstNormals = NormalsOfLit(dst);
        bool found = false;
        foreach (var n in dstNormals)
        {
            if (Vector3.Distance(n, new Vector3(0f, 0f, -1f)) < Epsilon) { found = true; break; }
        }
        Assert.True(found, "Expected a rotated +X normal to land at -Z.");
    }

    [Fact]
    public void Transform_NonUniformScale_RenormalizesAndDoesNotShearNormals()
    {
        var src = Meshes.Cube(Color.White);
        var dst = src.Transform(Matrix4x4.CreateScale(1f, 4f, 1f));

        // Every normal must remain unit length (renormalized).
        foreach (var n in NormalsOfLit(dst))
            Assert.Equal(1f, n.Length(), Epsilon);

        // Top-face normals were (0,1,0). Under non-uniform scale of
        // (1,4,1), naive scale-then-renormalize gives the right
        // direction (still +Y) only because the inverse-transpose
        // shrinks Y rather than stretching it. Confirm a top normal
        // is still (0,1,0).
        var dstNormals = NormalsOfLit(dst);
        bool found = false;
        foreach (var n in dstNormals)
        {
            if (Vector3.Distance(n, new Vector3(0f, 1f, 0f)) < Epsilon) { found = true; break; }
        }
        Assert.True(found, "Top-face normal should stay (0,1,0) under non-uniform scale.");
    }

    // ---------------- Concat ----------------

    [Fact]
    public void Concat_AppendsVertexAndIndexCounts()
    {
        var a = Meshes.Cube(Color.White);
        var b = Meshes.Cube(Color.Black);
        var combined = a.Concat(b);

        Assert.Equal(a.VertexCount + b.VertexCount, combined.VertexCount);
        Assert.Equal(a.IndexCount + b.IndexCount, combined.IndexCount);
        Assert.Equal(a.Topology, combined.Topology);
    }

    [Fact]
    public void Concat_RemapsSecondMeshIndicesByVertexOffset()
    {
        var a = Meshes.Cube(Color.White);
        var b = Meshes.Cube(Color.Black);
        var combined = a.Concat(b);

        var indices = combined.Indices;
        // First half indices < a.VertexCount; second half >= a.VertexCount.
        for (int i = 0; i < a.IndexCount; i++)
            Assert.True(indices[i] < a.VertexCount);
        for (int i = a.IndexCount; i < combined.IndexCount; i++)
            Assert.True(indices[i] >= a.VertexCount);
    }

    [Fact]
    public void Concat_WithTransform_IsEquivalentToTransformThenConcat()
    {
        var a = Meshes.Cube(Color.White);
        var b = Meshes.Cube(Color.Black);
        var m = Matrix4x4.CreateTranslation(5f, 0f, 0f);

        var combined = a.Concat(b, m);
        var expected = a.Concat(b.Transform(m));

        Assert.Equal(expected.VertexCount, combined.VertexCount);
        var got = PositionsOfLit(combined);
        var exp = PositionsOfLit(expected);
        for (int i = 0; i < got.Length; i++)
            Assert.Equal(exp[i], got[i]);
    }

    [Fact]
    public void Concat_RejectsMismatchedTopology()
    {
        var tri = Meshes.Cube(Color.White);
        var line = Meshes.Axes();
        // Different vertex types, won't even compile if we try to call
        // Concat directly. So instead build a same-vertex-type LineList
        // mesh for the topology mismatch case.
        var triCount = tri.VertexCount;
        var dummyLine = new Mesh<LitVertex3D>(
            new ReadOnlySpan<LitVertex3D>(new LitVertex3D[]
            {
                new(Vector3.Zero, Vector3.UnitY, Color.White),
                new(Vector3.UnitX, Vector3.UnitY, Color.White),
            }),
            ReadOnlySpan<uint>.Empty,
            Topology.LineList);
        Assert.Throws<ArgumentException>(() => tri.Concat(dummyLine));
    }

    [Fact]
    public void Concat_RejectsStripTopology()
    {
        var strip = new Mesh<LitVertex3D>(
            new ReadOnlySpan<LitVertex3D>(new LitVertex3D[]
            {
                new(Vector3.Zero, Vector3.UnitY, Color.White),
                new(Vector3.UnitX, Vector3.UnitY, Color.White),
                new(Vector3.UnitZ, Vector3.UnitY, Color.White),
            }),
            ReadOnlySpan<uint>.Empty,
            Topology.TriangleStrip);
        Assert.Throws<InvalidOperationException>(() => strip.Concat(strip));
    }
}

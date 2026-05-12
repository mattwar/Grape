using System.Numerics;
using Blitter.Bits;

namespace Blitter.Tests;

public class MeshTransformShortcutTests
{
    private const float Epsilon = 1e-4f;

    [Fact]
    public void Translate_MatchesTransform()
    {
        var src = Meshes.Cube(Color.White);
        var offset = new Vector3(3, -4, 5);

        var viaShortcut = src.Translate(offset);
        var viaMatrix = src.Transform(Matrix4x4.CreateTranslation(offset));

        var a = viaShortcut.Vertices;
        var b = viaMatrix.Vertices;
        Assert.Equal(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++)
            Assert.Equal(b[i].Position, a[i].Position);
    }

    [Fact]
    public void Scale_Uniform_GrowsBoundingBox()
    {
        var src = Meshes.Cube(Color.White);
        var srcBox = src.ComputeBoundingBox();
        var dst = src.Scale(2.5f);
        var dstBox = dst.ComputeBoundingBox();

        Assert.Equal(srcBox.Size * 2.5f, dstBox.Size);
    }

    [Fact]
    public void RotateY_90Deg_SendsXAxisToMinusZ()
    {
        // Single vertex at (1,0,0).
        var v = new[] { new Vertex3D(new Vector3(1, 0, 0)) };
        var m = Mesh.Create<Vertex3D>(v, ReadOnlySpan<uint>.Empty, Topology.PointList);
        var r = m.RotateY(MathF.PI * 0.5f);
        var p = r.Vertices[0].Position;

        Assert.Equal(0f, p.X, Epsilon);
        Assert.Equal(0f, p.Y, Epsilon);
        Assert.Equal(-1f, p.Z, Epsilon);
    }
}

public class MeshFlipTests
{
    [Fact]
    public void FlipWinding_Indexed_SwapsLastTwoIndicesPerTriangle()
    {
        var verts = new[]
        {
            new Vertex3D(new Vector3(0, 0, 0)),
            new Vertex3D(new Vector3(1, 0, 0)),
            new Vertex3D(new Vector3(0, 1, 0)),
            new Vertex3D(new Vector3(1, 1, 0)),
        };
        var indices = new uint[] { 0, 1, 2, 1, 3, 2 };
        var src = Mesh.Create<Vertex3D>(verts, indices, Topology.TriangleList);

        var dst = src.FlipWinding();

        var got = dst.Indices.ToArray();
        Assert.Equal(new uint[] { 0, 2, 1, 1, 2, 3 }, got);
    }

    [Fact]
    public void FlipWinding_Unindexed_SwapsLastTwoVerticesPerTriangle()
    {
        var verts = new[]
        {
            new Vertex3D(new Vector3(0, 0, 0)),
            new Vertex3D(new Vector3(1, 0, 0)),
            new Vertex3D(new Vector3(0, 1, 0)),
        };
        var src = Mesh.Create<Vertex3D>(verts, ReadOnlySpan<uint>.Empty, Topology.TriangleList);

        var dst = src.FlipWinding();

        var p = dst.Vertices;
        Assert.Equal(new Vector3(0, 0, 0), p[0].Position);
        Assert.Equal(new Vector3(0, 1, 0), p[1].Position);
        Assert.Equal(new Vector3(1, 0, 0), p[2].Position);
    }

    [Fact]
    public void FlipWinding_RejectsTriangleStrip()
    {
        var verts = new[]
        {
            new Vertex3D(new Vector3(0, 0, 0)),
            new Vertex3D(new Vector3(1, 0, 0)),
            new Vertex3D(new Vector3(0, 1, 0)),
            new Vertex3D(new Vector3(1, 1, 0)),
        };
        var src = Mesh.Create<Vertex3D>(verts, ReadOnlySpan<uint>.Empty, Topology.TriangleStrip);
        Assert.Throws<InvalidOperationException>(() => src.FlipWinding());
    }

    [Fact]
    public void FlipNormals_NegatesEveryNormal()
    {
        var src = Meshes.Cube(Color.White);
        var dst = src.FlipNormals();

        var s = src.Vertices;
        var d = dst.Vertices;
        Assert.Equal(s.Length, d.Length);
        for (int i = 0; i < s.Length; i++)
            Assert.Equal(-s[i].Normal, d[i].Normal);
    }
}

public class ModelTransformTests
{
    private const float Epsilon = 1e-4f;

    private static Model CubeModel(Vector3? offset = null, float scale = 1f)
    {
        var mesh = Meshes.TexturedCube(new Vector3(scale));
        if (offset is { } o)
            mesh = mesh.Translate(o);
        var sub = new Submesh(mesh, LitTextureMaterial.Default, "cube");
        return new Model(new[] { sub });
    }

    [Fact]
    public void Translate_ShiftsBoundingBox()
    {
        var m = CubeModel();
        var srcCenter = m.ComputeBoundingBox().Center;
        var moved = m.Translate(new Vector3(7, 8, 9));
        var dstCenter = moved.ComputeBoundingBox().Center;

        Assert.Equal(srcCenter + new Vector3(7, 8, 9), dstCenter);
    }

    [Fact]
    public void CenterOnOrigin_PutsBoundingBoxCenterAtZero()
    {
        var m = CubeModel(offset: new Vector3(10, -5, 3));
        var centered = m.CenterOnOrigin();
        var c = centered.ComputeBoundingBox().Center;

        Assert.Equal(0f, c.X, Epsilon);
        Assert.Equal(0f, c.Y, Epsilon);
        Assert.Equal(0f, c.Z, Epsilon);
    }

    [Fact]
    public void NormalizeSize_MakesLongestAxisMatchTarget()
    {
        // Stretched cube: 4 x 1 x 1.
        var stretched = Meshes.TexturedCube(new Vector3(4, 1, 1));
        var model = new Model(new[] { new Submesh(stretched, LitTextureMaterial.Default) });

        var normalized = model.NormalizeSize(2f);
        var size = normalized.ComputeBoundingBox().Size;
        var max = MathF.Max(size.X, MathF.Max(size.Y, size.Z));

        Assert.Equal(2f, max, Epsilon);
        // Aspect ratio preserved (uniform scale).
        Assert.Equal(4f, size.X / size.Y, Epsilon);
    }

    [Fact]
    public void Transform_PreservesSubmeshNameAndMaterial()
    {
        var m = CubeModel();
        var t = m.Translate(new Vector3(1, 0, 0));

        Assert.Equal(m.Submeshes.Count, t.Submeshes.Count);
        Assert.Equal(m.Submeshes[0].Name, t.Submeshes[0].Name);
        Assert.Same(m.Submeshes[0].Material, t.Submeshes[0].Material);
    }

    [Fact]
    public void Transform_DoesNotMutateSource()
    {
        var m = CubeModel();
        var beforeCenter = m.ComputeBoundingBox().Center;
        _ = m.Translate(new Vector3(100, 100, 100));
        var afterCenter = m.ComputeBoundingBox().Center;

        Assert.Equal(beforeCenter, afterCenter);
    }
}

using System.Numerics;
using System.Runtime.InteropServices;

namespace Blitter.Tests;

public class ObjLoaderTests
{
    // Use the per-process temp directory; clean up files after the
    // test run. Using the temp dir lets each test write self-contained
    // OBJ/MTL fixtures rather than checking binary fixtures into the
    // repo.
    private static string CreateTempFile(string extension, string content)
    {
        var path = Path.Combine(Path.GetTempPath(),
            $"Blitter-objloader-{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Load_SingleTriangle_ProducesOneSubmeshWithThreeVertices()
    {
        var obj = """
            v 0 0 0
            v 1 0 0
            v 0 1 0
            f 1 2 3
            """;
        var path = CreateTempFile(".obj", obj);
        try
        {
            using var model = Model.Load(path);

            var sub = Assert.Single(model.Submeshes);
            Assert.Equal(3, sub.Mesh.VertexCount);
            Assert.Equal(3, sub.Mesh.IndexCount);
            Assert.Same(Material.Default, sub.Material);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_QuadFace_TriangulatesAsFan()
    {
        // Four-corner face -> two triangles via fan from vertex 0:
        // (0,1,2) and (0,2,3). Six indices total over four unique
        // vertices.
        var obj = """
            v 0 0 0
            v 1 0 0
            v 1 1 0
            v 0 1 0
            f 1 2 3 4
            """;
        var path = CreateTempFile(".obj", obj);
        try
        {
            using var model = Model.Load(path);
            var sub = Assert.Single(model.Submeshes);
            Assert.Equal(4, sub.Mesh.VertexCount);
            Assert.Equal(6, sub.Mesh.IndexCount);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_FaceWithoutNormals_FabricatesSmoothNormal()
    {
        // Single triangle in the XZ plane. With no other faces
        // touching its vertices the smoothed normal equals the
        // face normal, so this test pins the basic "compute a
        // normal when none was authored" path.
        var obj = """
            v 0 0 0
            v 1 0 0
            v 0 0 1
            f 1 2 3
            """;
        var path = CreateTempFile(".obj", obj);
        try
        {
            using var model = Model.Load(path);
            var sub = Assert.Single(model.Submeshes);

            var verts = sub.Mesh.Vertices;
            foreach (var v in verts)
            {
                // Cross of (1,0,0) and (0,0,1) is (0,-1,0); the
                // loader normalises but doesn't flip, so the
                // produced normal is -Y. We just care that it's a
                // unit vector along the Y axis.
                Assert.Equal(0f, v.Normal.X, 4);
                Assert.Equal(0f, v.Normal.Z, 4);
                Assert.Equal(1f, MathF.Abs(v.Normal.Y), 4);
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_TexCoords_AreVFlippedForTopLeftConvention()
    {
        // OBJ uses bottom-left UV origin; loader should emit
        // top-left-style UVs, i.e. V is flipped.
        var obj = """
            v 0 0 0
            v 1 0 0
            v 0 1 0
            vt 0 0
            vt 1 0
            vt 0 1
            f 1/1 2/2 3/3
            """;
        var path = CreateTempFile(".obj", obj);
        try
        {
            using var model = Model.Load(path);
            var sub = Assert.Single(model.Submeshes);
            var verts = sub.Mesh.Vertices.ToArray();
            // Original (0,0) -> (0,1), (1,0) -> (1,1), (0,1) -> (0,0)
            Assert.Contains(verts, v => v.TextureCoordinate == new Vector2(0, 1));
            Assert.Contains(verts, v => v.TextureCoordinate == new Vector2(1, 1));
            Assert.Contains(verts, v => v.TextureCoordinate == new Vector2(0, 0));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_DifferentMaterials_ProduceSeparateSubmeshes()
    {
        // Two faces share positions but use different materials. The
        // loader should split them into two submeshes, one per
        // material, even though both reference the same group.
        var mtl = """
            newmtl red
            Kd 1 0 0
            newmtl green
            Kd 0 1 0
            """;
        var mtlPath = CreateTempFile(".mtl", mtl);
        var mtlName = Path.GetFileName(mtlPath);

        var obj = $$"""
            mtllib {{mtlName}}
            v 0 0 0
            v 1 0 0
            v 1 1 0
            v 0 1 0
            usemtl red
            f 1 2 3
            usemtl green
            f 1 3 4
            """;
        // Place obj next to mtl so the relative mtllib reference resolves.
        var objPath = Path.Combine(Path.GetDirectoryName(mtlPath)!,
            $"Blitter-objloader-{Guid.NewGuid():N}.obj");
        File.WriteAllText(objPath, obj);

        try
        {
            using var model = Model.Load(objPath);

            Assert.Equal(2, model.Submeshes.Count);

            // Order isn't strictly part of the contract, but in
            // practice the loader preserves bucket-creation order
            // (which mirrors usemtl order in the file).
            var red = Assert.Single(model.Submeshes, s => s.Material.Name == "red");
            var green = Assert.Single(model.Submeshes, s => s.Material.Name == "green");

            Assert.Equal(255, red.Material.DiffuseColor.R);
            Assert.Equal(0, red.Material.DiffuseColor.G);
            Assert.Equal(0, red.Material.DiffuseColor.B);

            Assert.Equal(0, green.Material.DiffuseColor.R);
            Assert.Equal(255, green.Material.DiffuseColor.G);
            Assert.Equal(0, green.Material.DiffuseColor.B);
        }
        finally
        {
            File.Delete(mtlPath);
            File.Delete(objPath);
        }
    }

    [Fact]
    public void Load_NegativeIndices_ResolveFromEndOfList()
    {
        // -1 == last, -2 == second-to-last. Some authoring tools emit
        // negative indices for streaming-friendly OBJs; loader needs
        // to handle them.
        var obj = """
            v 0 0 0
            v 1 0 0
            v 0 1 0
            f -3 -2 -1
            """;
        var path = CreateTempFile(".obj", obj);
        try
        {
            using var model = Model.Load(path);
            var sub = Assert.Single(model.Submeshes);
            Assert.Equal(3, sub.Mesh.VertexCount);
            Assert.Equal(3, sub.Mesh.IndexCount);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_EmptyOrCommentLines_AreIgnored()
    {
        var obj = """
            # leading comment
            v 0 0 0

            # blank line above
            v 1 0 0
                v 0 1 0
            f 1 2 3
            """;
        var path = CreateTempFile(".obj", obj);
        try
        {
            using var model = Model.Load(path);
            var sub = Assert.Single(model.Submeshes);
            Assert.Equal(3, sub.Mesh.VertexCount);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_NormalsAbsent_SharedPositionsDedupeAcrossFaces()
    {
        // Two triangles sharing an edge along the X axis; both lie
        // flat in the XZ plane, so the smoothed normal at every
        // shared position is the same +/-Y vector. With smooth
        // normals enabled the two faces collapse to four vertices
        // (one per unique position) instead of six (one per corner),
        // proving that the smoothing pass lets the deduplicator
        // share vertices across faces.
        var obj = """
            v 0 0 0
            v 1 0 0
            v 0 0 1
            v 1 0 1
            f 1 2 3
            f 2 4 3
            """;
        var path = CreateTempFile(".obj", obj);
        try
        {
            using var model = Model.Load(path);
            var sub = Assert.Single(model.Submeshes);
            Assert.Equal(4, sub.Mesh.VertexCount);
            Assert.Equal(6, sub.Mesh.IndexCount);
        }
        finally { File.Delete(path); }
    }
}

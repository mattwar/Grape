using System.Numerics;
using Blitter.Bits;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;

namespace Blitter.Tests;

public class GltfLoaderTests
{
    private static string CreateTempPath(string extension)
    {
        return Path.Combine(Path.GetTempPath(),
            $"Blitter-gltfloader-{Guid.NewGuid():N}{extension}");
    }

    /// <summary>
    /// Build a single-triangle glb with a colored material at the
    /// supplied path. Returns the path for cleanup.
    /// </summary>
    private static void WriteSingleTriangleGlb(string path, Vector4 baseColor)
    {
        var mat = new MaterialBuilder("tri")
            .WithMetallicRoughnessShader()
            .WithBaseColor(baseColor);

        var mesh = new MeshBuilder<VertexPositionNormal>("tri");
        var prim = mesh.UsePrimitive(mat);
        var n = Vector3.UnitZ;
        prim.AddTriangle(
            new VertexPositionNormal(new Vector3(0, 0, 0), n),
            new VertexPositionNormal(new Vector3(1, 0, 0), n),
            new VertexPositionNormal(new Vector3(0, 1, 0), n));

        var scene = new SceneBuilder();
        scene.AddRigidMesh(mesh, Matrix4x4.Identity);
        scene.ToGltf2().SaveGLB(path);
    }

    [Fact]
    public void Load_SingleTriangle_ProducesOneSubmeshWithThreeVertices()
    {
        var path = CreateTempPath(".glb");
        WriteSingleTriangleGlb(path, new Vector4(1, 1, 1, 1));
        try
        {
            var model = Model.Load(path);
            var sub = Assert.Single(model.Parts);
            Assert.Equal(3, sub.Mesh.VertexCount);
            Assert.Equal(3, sub.Mesh.IndexCount);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_BaseColorFactor_PropagatesToMaterialDiffuseColor()
    {
        var path = CreateTempPath(".glb");
        // Pure red, full alpha.
        WriteSingleTriangleGlb(path, new Vector4(1f, 0f, 0f, 1f));
        try
        {
            var model = Model.Load(path);
            var sub = Assert.Single(model.Parts);
            var mat = Assert.IsType<PbrMaterial>(sub.Material);
            Assert.Equal(255, mat.BaseColor.R);
            Assert.Equal(0, mat.BaseColor.G);
            Assert.Equal(0, mat.BaseColor.B);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_NodeWithTranslation_BakesIntoVertexPositions()
    {
        // Build a triangle, place its node at +5 on X, then verify
        // the loaded vertices are translated (loader is supposed to
        // bake node.WorldMatrix into positions).
        var path = CreateTempPath(".glb");
        var mat = new MaterialBuilder("tri").WithMetallicRoughnessShader();
        var mesh = new MeshBuilder<VertexPositionNormal>("tri");
        var prim = mesh.UsePrimitive(mat);
        var n = Vector3.UnitZ;
        prim.AddTriangle(
            new VertexPositionNormal(Vector3.Zero, n),
            new VertexPositionNormal(Vector3.UnitX, n),
            new VertexPositionNormal(Vector3.UnitY, n));

        var scene = new SceneBuilder();
        scene.AddRigidMesh(mesh, Matrix4x4.CreateTranslation(5, 0, 0));
        scene.ToGltf2().SaveGLB(path);

        try
        {
            var model = Model.Load(path);
            var sub = Assert.Single(model.Parts);
            // We don't peek inside the GPU buffer; rely on the fact
            // that the internal Mesh<T> exposes VertexCount and our
            // load went through the bake path. Stronger assertions
            // would require an internal accessor; this test just
            // pins that loading with a non-identity transform
            // succeeds end-to-end.
            Assert.Equal(3, sub.Mesh.VertexCount);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_UnsupportedExtension_Throws()
    {
        Assert.Throws<NotSupportedException>(() => Model.Load("file.xyz"));
    }

    [Fact]
    public void Load_GltfExtension_AlsoSupported()
    {
        // Same fixture, but as the JSON-based .gltf form. SharpGLTF's
        // SaveGLTF produces .gltf + .bin sidecars in the same folder.
        var dir = Directory.CreateTempSubdirectory("Blitter-gltfloader-text");
        var path = Path.Combine(dir.FullName, "tri.gltf");
        try
        {
            var mat = new MaterialBuilder("tri").WithMetallicRoughnessShader();
            var mesh = new MeshBuilder<VertexPositionNormal>("tri");
            var prim = mesh.UsePrimitive(mat);
            var n = Vector3.UnitZ;
            prim.AddTriangle(
                new VertexPositionNormal(Vector3.Zero, n),
                new VertexPositionNormal(Vector3.UnitX, n),
                new VertexPositionNormal(Vector3.UnitY, n));
            var scene = new SceneBuilder();
            scene.AddRigidMesh(mesh, Matrix4x4.Identity);
            scene.ToGltf2().SaveGLTF(path);

            var model = Model.Load(path);
            Assert.Single(model.Parts);
        }
        finally { dir.Delete(recursive: true); }
    }
}

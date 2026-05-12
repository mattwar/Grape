#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/LoadObjModel.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Demonstrates loading a 3D model from a Wavefront OBJ file via
// Model.Load(). Real-world workflow: export an .obj from your favorite
// DCC tool (Blender, Maya, ...) -- with or without an accompanying
// .mtl sidecar -- and load it with one call.
//
// To keep this sample self-contained the OBJ + MTL are written to a
// temp directory at runtime; in normal use you'd point Model.Load at
// a path on disk. Replace `objPath` with your own file to load it.

using System.Numerics;
using Blitter;
using Blitter.Bits;

// A small two-material model: an octahedron (bipyramid) with the four
// upper faces in one color and the four lower faces in another. Two
// materials -> two parts -> two draw calls under the hood, all
// invisible to the caller.
const string Obj = """
    # Bipyramid centered on origin, ±1 along each axis.
    mtllib gem.mtl
    o Gem

    # Equatorial ring (+X, +Z, -X, -Z) and the two apex points.
    v  1  0  0
    v  0  0  1
    v -1  0  0
    v  0  0 -1
    v  0  1  0
    v  0 -1  0

    # Upper four faces.
    usemtl Crown
    f 1 2 5
    f 2 3 5
    f 3 4 5
    f 4 1 5

    # Lower four faces. Note the winding flip so each face's outward
    # normal points away from the origin.
    usemtl Pavilion
    f 1 6 2
    f 2 6 3
    f 3 6 4
    f 4 6 1
    """;

const string Mtl = """
    # Two flat colors. No textures.
    newmtl Crown
    Kd 0.95 0.55 0.20

    newmtl Pavilion
    Kd 0.20 0.55 0.95
    """;

// Write fixtures to a temp directory; clean them up on exit. In a
// real application Model.Load just takes a path to your authored OBJ.
var tempDir = Directory.CreateTempSubdirectory("Blitter-objsample");
var objPath = Path.Combine(tempDir.FullName, "gem.obj");
var mtlPath = Path.Combine(tempDir.FullName, "gem.mtl");
File.WriteAllText(objPath, Obj);
File.WriteAllText(mtlPath, Mtl);

var model = Model.Load(objPath);
Console.WriteLine($"Loaded {objPath}: {model.Parts.Length} parts");
foreach (var sub in model.Parts)
{
    var lit = (LitTextureMaterial)sub.Material;
    Console.WriteLine(
        $"  '{sub.Name}': {sub.Mesh.VertexCount} verts, " +
        $"material '{lit.Name}' " +
        $"Kd=({lit.DiffuseColor.R},{lit.DiffuseColor.G},{lit.DiffuseColor.B})");
}

var window = new Window3D
{
    Title = "Loaded OBJ: two-material bipyramid",
    BackgroundColor = new Color(8, 8, 24),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 1.5f, 4.5f),
    Target = Vector3.Zero,
};

await window.RunAsync(rd =>
{
    rd.Camera = camera;

    // Soft ambient; an orbiting directional light so every face gets
    // its turn in shadow as the gem spins.
    rd.AmbientLight = new Color(40, 40, 60);
    var t = rd.ElapsedSecondsSinceStart;
    rd.DirectionalLight = new DirectionalLight(
        Vector3.Normalize(new Vector3(MathF.Cos(t * 0.4f), 0.6f, MathF.Sin(t * 0.4f))),
        Color.White);

    // Spin the gem around its vertical axis.
    var transform = Matrix4x4.CreateRotationY(t * 0.7f)
        .RotateX(MathF.Sin(t * 0.5f) * 0.2f);

    using (rd.PushState())
    {
        rd.CullMode = CullMode.Back;
        // One call draws every part; the default materializer walks
        // them internally, picks Shaders.LitTexture for each
        // LitTextureMaterial, and binds the material texture (or a 1x1
        // white fallback) per part.
        rd.DrawModel(model, transform);
    }
});

try { tempDir.Delete(recursive: true); } catch { /* leave the temp files if cleanup fails */ }

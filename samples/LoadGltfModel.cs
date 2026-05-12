#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/LoadGltfModel.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Demonstrates loading a 3D model from a glTF 2.0 file (`.glb` /
// `.gltf`) via Model.Load(). Real-world workflow: export from Blender,
// download a Khronos sample asset, grab something off Sketchfab --
// then load with one call.
//
// To keep this sample self-contained we build a tiny textured cube
// at runtime using SharpGLTF and save it to a temp `.glb`. In normal
// use you'd just point Model.Load at your own file.

using System.Numerics;
using Blitter;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;

// --- Build a procedural textured-cube glb -----------------------------

var tempDir = Directory.CreateTempSubdirectory("Blitter-gltfsample");
var texPath = Path.Combine(tempDir.FullName, "checker.png");
WriteCheckerboardPng(texPath, size: 128, cells: 8);

var material = new MaterialBuilder("Checker")
    .WithMetallicRoughnessShader()
    .WithBaseColor(texPath);

// Cube via 6 quads with matching UVs so the checker tiles per face.
var mesh = new MeshBuilder<VertexPositionNormal, VertexTexture1>("cube");
AddQuad(mesh, material, new Vector3( 1, 0, 0), Vector3.UnitX);
AddQuad(mesh, material, new Vector3(-1, 0, 0), -Vector3.UnitX);
AddQuad(mesh, material, new Vector3( 0, 1, 0), Vector3.UnitY);
AddQuad(mesh, material, new Vector3( 0,-1, 0), -Vector3.UnitY);
AddQuad(mesh, material, new Vector3( 0, 0, 1), Vector3.UnitZ);
AddQuad(mesh, material, new Vector3( 0, 0,-1), -Vector3.UnitZ);

var scene = new SceneBuilder();
scene.AddRigidMesh(mesh, Matrix4x4.Identity);
var glbPath = Path.Combine(tempDir.FullName, "cube.glb");
scene.ToGltf2().SaveGLB(glbPath);

// --- Load + render ----------------------------------------------------

using var model = Model.Load(glbPath);

var window = new Window3D
{
    Title = "Loaded glTF: textured cube",
    BackgroundColor = new Color(8, 8, 24),
    //FullScreen = true,
    CloseKey = Key.Escape,
    AutoInvalidate = true,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(2.2f, 2.0f, 4.5f),
    Target = Vector3.Zero,
};

window.Rendering += (w, rd) =>
{
    rd.Camera = camera;
    rd.AmbientLight = new Color(40, 40, 60);
    var t = rd.ElapsedSecondsSinceStart;
    rd.DirectionalLight = new DirectionalLight(
        Vector3.Normalize(new Vector3(MathF.Cos(t * 0.4f), 0.6f, MathF.Sin(t * 0.4f))),
        Color.White);

    var transform =
        Matrix4x4.CreateRotationY(t * 0.7f) *
        Matrix4x4.CreateRotationX(MathF.Sin(t * 0.5f) * 0.2f);

    using (rd.PushState())
    {
        rd.CullMode = CullMode.Back;
        model.Draw(rd, transform);
    }
};

try
{
    await window.WaitForCloseAsync();
}
finally
{
    try { tempDir.Delete(recursive: true); } catch { /* leave temp files if cleanup fails */ }
}

// --- helpers ---------------------------------------------------------

static void AddQuad(
    MeshBuilder<VertexPositionNormal, VertexTexture1> mesh,
    MaterialBuilder material,
    Vector3 center,
    Vector3 normal)
{
    // Pick two in-plane axes orthogonal to the normal.
    var u = Vector3.Cross(normal, MathF.Abs(normal.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX);
    u = Vector3.Normalize(u);
    var v = Vector3.Normalize(Vector3.Cross(normal, u));

    var p0 = center - u - v;
    var p1 = center + u - v;
    var p2 = center + u + v;
    var p3 = center - u + v;

    var prim = mesh.UsePrimitive(material);
    prim.AddTriangle(
        new(new VertexPositionNormal(p0, normal), new VertexTexture1(new Vector2(0, 1))),
        new(new VertexPositionNormal(p1, normal), new VertexTexture1(new Vector2(1, 1))),
        new(new VertexPositionNormal(p2, normal), new VertexTexture1(new Vector2(1, 0))));
    prim.AddTriangle(
        new(new VertexPositionNormal(p0, normal), new VertexTexture1(new Vector2(0, 1))),
        new(new VertexPositionNormal(p2, normal), new VertexTexture1(new Vector2(1, 0))),
        new(new VertexPositionNormal(p3, normal), new VertexTexture1(new Vector2(0, 0))));
}

static void WriteCheckerboardPng(string path, int size, int cells)
{
    using var image = Image.Create(size, size, PixelFormat.ABGR8888);
    var cell = size / cells;
    var dark = new Color(40, 60, 220);
    var light = new Color(220, 70, 80);
    for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool on = ((x / cell) + (y / cell)) % 2 == 0;
            image.SetPixel(x, y, on ? light : dark);
        }
    image.Save(path);
}

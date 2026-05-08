#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/StanfordBunny.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// Renders one of two classic graphics test models -- the Stanford
// Bunny (~70K triangles) or the Utah Teapot (~6K triangles) -- loaded
// from a Wavefront OBJ file. Switch the file constant below to flip
// between them.
//
// Both meshes are positions-and-faces only: no normals, no UVs, no
// material library. The OBJ loader smooths a normal per *position*
// when `vn` is absent (area-weighted average of incident face
// normals), so adjacent triangles read continuously across curved
// surfaces instead of as visible facets.
//
// Models courtesy of:
//   bunny.obj  -- Stanford Computer Graphics Laboratory (zipper'd
//                 reconstruction of the Stanford Bunny scan).
//   teapot.obj -- Martin Newell's Utah Teapot (public domain).
// Both via Alec Jacobson's common-3d-test-models repo.

using System.Numerics;
using System.Runtime.CompilerServices;
using Grape;

//const string ModelFile = "teapot.obj";
const string ModelFile = "bunny.obj";

static string SampleAsset(string name, [CallerFilePath] string sourcePath = "")
    => Path.Combine(Path.GetDirectoryName(sourcePath)!, name);

using var model = Model.Load(SampleAsset(ModelFile));

// Center + scale the model into a unit-ish bounding sphere so it
// frames the same way regardless of which classic asset is loaded
// (bunny is millimeter-scale and offset; teapot is ~6 units wide).
var (center, radius) = ComputeBounds(model);
var fitTransform =
    Matrix4x4.CreateTranslation(-center) *
    Matrix4x4.CreateScale(1f / radius);

Console.WriteLine(
    $"Loaded {model.SourcePath}: {model.Submeshes.Count} submesh(es), " +
    $"{model.Submeshes.Sum(s => s.Mesh.VertexCount)} verts; " +
    $"centered at {center}, fit radius {radius:F3}.");

var window = new Window3D
{
    Title = $"Loaded model: {ModelFile}",
    BackgroundColor = new Color(8, 8, 24),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 0.4f, 2.6f),
    Target = Vector3.Zero,
};

// Drag the left mouse button to orbit; scroll to zoom.
var orbiter = new CameraOrbiter(window)
{
    Camera = camera,
    Distance = 2.6f,
    Pitch = 0.15f,
};

window.Rendering += (w, rd) =>
{
    orbiter.Update(rd.GetUpdateContext());
    orbiter.Draw(rd);

    rd.AmbientLight = new Color(40, 40, 60);

    // Slow-orbiting key light so the silhouette and faceting both get
    // their turn to be visible.
    var t = (float)rd.ElapsedSinceStart.TotalSeconds;
    rd.DirectionalLight = new DirectionalLight(
        Vector3.Normalize(new Vector3(MathF.Cos(t * 0.4f), 0.6f, MathF.Sin(t * 0.4f))),
        Color.White);

    using (rd.PushState())
    {
        rd.CullMode = CullMode.Back;
        model.Draw(rd, fitTransform);
    }

    w.Invalidate();
};

await window.WaitForCloseAsync();

// --- helpers ------------------------------------------------------

static (Vector3 Center, float Radius) ComputeBounds(Model model)
{
    var min = new Vector3(float.PositiveInfinity);
    var max = new Vector3(float.NegativeInfinity);
    foreach (var sub in model.Submeshes)
    {
        // use the vertices to determine the bounding box
        foreach (var v in sub.Mesh.Vertices)
        {
            min = Vector3.Min(min, v.Position);
            max = Vector3.Max(max, v.Position);
        }
    }
    var center = (min + max) * 0.5f;
    var radius = (max - min).Length() * 0.5f;
    return (center, radius == 0f ? 1f : radius);
}

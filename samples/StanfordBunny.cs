#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/StanfordBunny.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

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
using Blitter;
using Blitter.Bits;

//const string ModelFile = "teapot.obj";
const string ModelFile = "bunny.obj";

var model = Model.Load(Asset.GetPathRelativeToCaller(ModelFile));

// Center + scale the model into a unit-ish bounding sphere so it
// frames the same way regardless of which classic asset is loaded
// (bunny is millimeter-scale and offset; teapot is ~6 units wide).
var sphere = model.ComputeBoundingSphere();
var fitTransform = Matrix4x4.CreateTranslation(-sphere.Center)
    .Scale(1f / (sphere.Radius == 0f ? 1f : sphere.Radius));

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

await window.RunAsync(rd =>
{
    orbiter.Update(rd.GetUpdateContext());
    orbiter.Draw(rd);

    rd.AmbientLight = new Color(40, 40, 60);

    // Slow-orbiting key light so the silhouette and faceting both get their turn to be visible.
    var t = rd.ElapsedSecondsSinceStart;
    rd.DirectionalLight = new DirectionalLight(
        Vector3.Normalize(MathG.Orbit(t, speed: 0.4f) + Vector3.UnitY * 0.6f),
        Color.White);

    using (rd.PushState())
    {
        rd.CullMode = CullMode.Back;
        rd.DrawModel(model, fitTransform);
    }
});

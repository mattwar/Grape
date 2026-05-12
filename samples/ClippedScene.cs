#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/ClippedScene.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// The samples/NuGet.config in this folder pulls Blitter from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.

// A moving rectangular "porthole" over a 3D scene, demonstrating
// Renderer3D.ClipRect. Unlike Viewport, ClipRect doesn't scale or
// reposition the scene -- it just masks which pixels of the render
// target the draw is allowed to write.

using System.Numerics;
using Blitter;
using Blitter.Bits;

static Mesh<ColorVertex3D> MakeTetrahedron(Color c0, Color c1, Color c2, Color c3)
{
    var v0 = new Vertex3D( 1f,  1f,  1f);
    var v1 = new Vertex3D( 1f, -1f, -1f);
    var v2 = new Vertex3D(-1f,  1f, -1f);
    var v3 = new Vertex3D(-1f, -1f,  1f);

    return Mesh.Create([
        new ColorVertex3D(v1, c1), new ColorVertex3D(v2, c2), new ColorVertex3D(v3, c3),
        new ColorVertex3D(v0, c0), new ColorVertex3D(v3, c3), new ColorVertex3D(v2, c2),
        new ColorVertex3D(v0, c0), new ColorVertex3D(v1, c1), new ColorVertex3D(v3, c3),
        new ColorVertex3D(v0, c0), new ColorVertex3D(v2, c2), new ColorVertex3D(v1, c1),
    ]);
}

var tetra = MakeTetrahedron(
    new Color(255, 64, 64),
    new Color(255, 200, 0),
    new Color(64, 200, 255),
    new Color(160, 64, 255));

var window = new Window3D
{
    Title = "Clipped Scene (clip-rect demo)",
    BackgroundColor = new Color(0, 0, 32),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 0.6f, 5f),
};

await window.RunAsync(rd =>
{
    var t = rd.ElapsedSecondsSinceStart;
    var (width, height) = window.Size;
    var viewProjection = camera.GetViewProjection(rd);

    var spin = Matrix4x4.CreateRotationY(t).RotateX(t * 0.7f);
    var transform = spin * viewProjection;

    // A "ghost" pass with no clip draws the whole tetra at low intensity
    // so it's obvious where the spinning shape is even when masked away.
    // Technically achieved by drawing in wireframe so no surface fill
    // competes with the clipped solid pass below.
    using (rd.PushState())
    {
        rd.Wireframe = true;
        rd.DrawMesh(tetra, Shaders.PositionColorWithTransform, transform);
    }

    // The "porthole" slides horizontally across the window. ClipRect is
    // applied per-draw (snapshotted at queue time) so we set it before
    // the solid pass and the GPU scissors away anything outside.
    var portholeW = width / 3f;
    var portholeH = height * 0.7f;
    var travel = (width + portholeW) * (0.5f + 0.5f * MathF.Sin(t * 0.6f)) - portholeW;
    var porthole = new Rect(travel, (height - portholeH) / 2f, portholeW, portholeH);

    using (rd.PushState())
    {
        rd.ClipRect = porthole;
        rd.DrawMesh(tetra, Shaders.PositionColorWithTransform, transform);
    }
});
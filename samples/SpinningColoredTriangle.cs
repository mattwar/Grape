#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/SpinningColoredTriangle.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// The samples/NuGet.config in this folder pulls Grape.Graphics from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.

using System.Numerics;
using Grape;

// A colored triangle in model space (centered at the origin, ~1 unit tall).
var triangle = Mesh.Create([
    new ColorVertex3D(new Vertex3D( 0.0f,  0.5f, 0f), new Color(255, 0,   0)),
    new ColorVertex3D(new Vertex3D( 0.5f, -0.5f, 0f), new Color(0,   255, 0)),
    new ColorVertex3D(new Vertex3D(-0.5f, -0.5f, 0f), new Color(0,   0,   255))
    ]);

var window = new Window3D
{
    Title = "Spinning Colored Triangle",
    BackgroundColor = new Color(0, 0, 32),
    FullScreen = true,
    CloseKey = Key.Escape,
};

window.Rendering += (w, r) =>
{
    var seconds = (float)r.ElapsedSinceStart.TotalSeconds;
    var (width, height) = w.Size;
    var aspect = (float)height / width;
    var transform =
        Matrix4x4.CreateRotationZ(seconds) *
        Matrix4x4.CreateScale(0.8f) *
        Matrix4x4.CreateScale(aspect, 1f, 1f);

    r.DrawMesh(triangle, Shaders.PositionColorWithTransform, transform);

    w.Invalidate(); // schedule the next frame
};

await window.WaitForCloseAsync();

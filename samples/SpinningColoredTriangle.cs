#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/SpinningColoredTriangle.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// The samples/NuGet.config in this folder pulls Blitter from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.

using System.Numerics;
using Blitter;

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

await window.RunAsync(rd =>
{
    var seconds = rd.ElapsedSecondsSinceStart;
    // Inverse aspect on X keeps the triangle proportional in a wide window.
    var transform =
        Matrix4x4.CreateRotationZ(seconds) *
        Matrix4x4.CreateScale(0.8f) *
        Matrix4x4.CreateScale(1f / rd.AspectRatio, 1f, 1f);

    rd.DrawMesh(triangle, Shaders.PositionColorWithTransform, transform);
});
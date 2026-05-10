#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run ManualRenderLoop.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// The samples/NuGet.config in this folder pulls Blitter from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.

using System.Numerics;
using Blitter;

var triangle = Mesh.Create([
    new ColorVertex3D(new Vertex3D( 0.0f,  0.5f, 0f), new Color(255, 0,   0)),
    new ColorVertex3D(new Vertex3D( 0.5f, -0.5f, 0f), new Color(0,   255, 0)),
    new ColorVertex3D(new Vertex3D(-0.5f, -0.5f, 0f), new Color(0,   0,   255))
    ]);

var window = new Window3D
{
    Title = "Manual Render Loop",
    BackgroundColor = new Color(0, 0, 32),
    FullScreen = true,
};

// Run until the user presses Escape (in addition to the default
// "window closed" exit). Any predicate works here.
await window.RunAsync(
    shouldContinue: () => !Keyboard.IsDown(Key.Escape),
    renderFrame: r =>
    {
        var seconds = (float)r.ElapsedSinceStart.TotalSeconds;
        var (width, height) = window.Size;
        var aspect = (float)height / width;
        var transform =
            Matrix4x4.CreateRotationZ(seconds) *
            Matrix4x4.CreateScale(0.8f) *
            Matrix4x4.CreateScale(aspect, 1f, 1f);

        r.DrawMesh(triangle, transform);
    });

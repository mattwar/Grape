#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run ManualRenderLoop.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// The samples/NuGet.config in this folder pulls Grape.Graphics from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.

using System.Numerics;
using Grape;
using Grape.Utilities;

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

// use periodic timer so we don't spin the CPU at 100%
// but also have even intervals between frames (vs. Task.Delay which can be uneven)
var timer = new AsyncPeriodicTimer(TimeSpan.FromSeconds(1.0 / 60));

while (!window.IsClosed && !Keyboard.IsDown(Key.Escape))
{
    window.Render(frame =>
    {
        var seconds = (float)frame.ElapsedSinceWindowCreated.TotalSeconds;
        var (width, height) = window.Size;
        var aspect = (float)height / width;
        var transform =
            Matrix4x4.CreateRotationZ(seconds) *
            Matrix4x4.CreateScale(0.8f) *
            Matrix4x4.CreateScale(aspect, 1f, 1f);

        frame.Renderer.RenderMesh(triangle, transform);
    });

    await timer.NextPeriod();
}

window.Close();

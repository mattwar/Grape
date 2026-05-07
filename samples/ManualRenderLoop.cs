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

var triangle = Mesh.Create([
    new ColorVertex3D(new Vertex3D( 0.0f,  0.5f, 0f), new Color(255, 0,   0)),
    new ColorVertex3D(new Vertex3D( 0.5f, -0.5f, 0f), new Color(0,   255, 0)),
    new ColorVertex3D(new Vertex3D(-0.5f, -0.5f, 0f), new Color(0,   0,   255))
    ]);

var window = new Window3D
{
    Title = "Manual Render Loop",
    BackgroundColor = new Color(0, 0, 32),
    FullScreen = true
};

while (!window.IsClosed && !Keyboard.IsDown(Key.Escape))
{
    var r = window.Renderer;
    var seconds = (float)r.ElapsedSinceStart.TotalSeconds;
    var (width, height) = window.Size;
    var aspect = (float)height / width;
    var transform =
        Matrix4x4.CreateRotationZ(seconds) *
        Matrix4x4.CreateScale(0.8f) *
        Matrix4x4.CreateScale(aspect, 1f, 1f);

    r.DrawMesh(triangle, transform);
    r.Render();

    await window.NextFrameAsync();
}

window.Close();

#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/TriangleSwarm.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// The samples/NuGet.config in this folder pulls Grape.Graphics from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.

using System.Collections.Immutable;
using System.Numerics;
using Grape;

const int Count = 24;

// One small triangle in model space, shared by every instance.
var triangle = ImmutableArray.Create(
    new ColorVertex3D(new Vertex3D( 0.0f,   0.12f, 0f), new Color(255,   0,   0)),
    new ColorVertex3D(new Vertex3D( 0.10f, -0.08f, 0f), new Color(  0, 255,   0)),
    new ColorVertex3D(new Vertex3D(-0.10f, -0.08f, 0f), new Color(  0,   0, 255)));

var window = new Window3D
{
    Title = "Triangle Swarm",
    BackgroundColor = new Color(8, 0, 24),
    FullScreen = true,
    CloseKey = Key.Escape,
};

window.Rendering += (w, e) =>
{
    var t = (float)e.ElapsedSinceWindowCreated.TotalSeconds;
    var (width, height) = w.Size;
    var aspect = (float)height / width;
    var aspectScale = Matrix4x4.CreateScale(aspect, 1f, 1f);

    // The orbit ring breathes in and out over time.
    float ring = 0.55f + 0.15f * MathF.Sin(t * 0.7f);

    for (int i = 0; i < Count; i++)
    {
        float phase = (float)i / Count;
        float orbitAngle = phase * MathF.Tau + t * 0.5f;
        float spinAngle  = phase * MathF.Tau + t * 2.5f;
        float bob        = 0.05f * MathF.Sin(t * 1.5f + phase * MathF.Tau * 2f);

        float cx = MathF.Cos(orbitAngle) * ring;
        float cy = MathF.Sin(orbitAngle) * ring + bob;

        var transform =
            Matrix4x4.CreateRotationZ(spinAngle) *
            Matrix4x4.CreateTranslation(cx, cy, 0f) *
            aspectScale;

        e.Renderer.RenderMesh(triangle, Shaders.PositionColorWithTransform, transform);
    }

    w.Invalidate(); // schedule the next frame
};

await window.WaitForCloseAsync();


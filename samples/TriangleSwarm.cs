#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/TriangleSwarm.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// Draws 24 copies of one triangle by issuing 24 separate DrawMesh calls, one per triangle, 
// each with its own transform. 

using System.Collections.Immutable;
using System.Numerics;
using Blitter;
using Blitter.Bits;

const int Count = 24;

// One small triangle in model space, shared by every draw.
var triangle = ImmutableArray.Create(
    new ColorVertex3D(new Vertex3D( 0.0f,   0.12f, 0f), Color.Red),
    new ColorVertex3D(new Vertex3D( 0.10f, -0.08f, 0f), Color.Green),
    new ColorVertex3D(new Vertex3D(-0.10f, -0.08f, 0f), Color.Blue));
var mesh = Mesh.Create(triangle.AsSpan());

var window = new Window3D
{
    Title = "Triangle Swarm",
    BackgroundColor = new Color(8, 0, 24),
    FullScreen = true,
    CloseKey = Key.Escape,
};

await window.RunAsync(rd =>
{
    var t = rd.ElapsedSecondsSinceStart;
    var aspectScale = Matrix4x4.CreateScale(1f / rd.AspectRatio, 1f, 1f);

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

        var transform = Matrix4x4.CreateRotationZ(spinAngle)
            .Translate(cx, cy, 0f) *
            aspectScale;

        rd.DrawMesh(mesh, Shaders.PositionColorWithTransform, transform);
    }
});
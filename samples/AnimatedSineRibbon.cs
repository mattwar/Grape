#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/AnimatedSineRibbon.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// The samples/NuGet.config in this folder pulls Blitter from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.

using System.Numerics;
using Blitter;

// Animated horizontal ribbon whose vertical position follows a travelling
// sine wave. The mesh is regenerated and re-uploaded every frame.
const int Segments = 128;
const float Width = 1.6f;        // total horizontal span in NDC
const float Thickness = 0.04f;   // ribbon thickness in NDC
const float Amplitude = 0.35f;
const float Frequency = 4f;      // wavelengths across the ribbon
const float Speed = 2f;          // travel speed

// Two triangles per segment, six vertices each.
var vertices = new ColorVertex3D[Segments * 6];
// Reusable mesh -- we mutate the vertex array in-place each frame and
// call mesh.Update(...) so the renderer re-uploads only the changed
// contents (Mesh's Version bump triggers that).
var mesh = Mesh.Create<ColorVertex3D>(vertices);

var window = new Window3D
{
    Title = "Animated Sine Ribbon",
    BackgroundColor = new Color(0, 0, 32),
    FullScreen = true,
    CloseKey = Key.Escape,
};

await window.RunAsync(rd =>
{
    var t = rd.ElapsedSecondsSinceStart;

    for (int i = 0; i < Segments; i++)
    {
        float u0 = (float)i / Segments;
        float u1 = (float)(i + 1) / Segments;

        float x0 = -Width / 2f + u0 * Width;
        float x1 = -Width / 2f + u1 * Width;

        float y0 = MathF.Sin(u0 * Frequency * MathF.Tau + t * Speed) * Amplitude;
        float y1 = MathF.Sin(u1 * Frequency * MathF.Tau + t * Speed) * Amplitude;

        var c0 = Color.FromHsv(u0 + t * 0.1f, 1f, 1f);
        var c1 = Color.FromHsv(u1 + t * 0.1f, 1f, 1f);

        var topLeft     = new ColorVertex3D(new Vertex3D(x0, y0 + Thickness, 0f), c0);
        var bottomLeft  = new ColorVertex3D(new Vertex3D(x0, y0 - Thickness, 0f), c0);
        var topRight    = new ColorVertex3D(new Vertex3D(x1, y1 + Thickness, 0f), c1);
        var bottomRight = new ColorVertex3D(new Vertex3D(x1, y1 - Thickness, 0f), c1);

        int v = i * 6;
        vertices[v + 0] = topLeft;
        vertices[v + 1] = bottomLeft;
        vertices[v + 2] = bottomRight;
        vertices[v + 3] = topLeft;
        vertices[v + 4] = bottomRight;
        vertices[v + 5] = topRight;
    }

    mesh.Update(vertices);

    // Squash to height/width so the ribbon keeps its aspect when the
    // window is wider than tall.
    var transform = Matrix4x4.CreateScale(1f / rd.AspectRatio, 1f, 1f);

    rd.DrawMesh(mesh, transform);
});
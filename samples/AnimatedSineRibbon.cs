#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/AnimatedSineRibbon.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     ./pack-local.ps1
//
// The samples/NuGet.config in this folder pulls Grape.Graphics from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.

using System.Numerics;
using Grape;

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

var window = new Window3D(800, 600)
{
    Title = "Animated Sine Ribbon",
    BackgroundColor = new Color(0, 0, 32),
    FullScreen = true
};

window.KeyDown += (_, e) =>
{
    if (e.Key == Key.Escape)
        window.Dispose();
};

window.RenderingFrame += (w, frame) =>
{
    var t = (float)frame.ElapsedSinceWindowCreated.TotalSeconds;

    for (int i = 0; i < Segments; i++)
    {
        float u0 = (float)i / Segments;
        float u1 = (float)(i + 1) / Segments;

        float x0 = -Width / 2f + u0 * Width;
        float x1 = -Width / 2f + u1 * Width;

        float y0 = MathF.Sin(u0 * Frequency * MathF.Tau + t * Speed) * Amplitude;
        float y1 = MathF.Sin(u1 * Frequency * MathF.Tau + t * Speed) * Amplitude;

        var c0 = HsvToColor(u0 + t * 0.1f, 1f, 1f);
        var c1 = HsvToColor(u1 + t * 0.1f, 1f, 1f);

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

    var (width, height) = w.Size;
    var aspect = (float)height / width;
    var transform = Matrix4x4.CreateScale(aspect, 1f, 1f);

    frame.Renderer.RenderMesh(vertices, Shaders.PositionColorTransform, transform);

    w.Invalidate(); // schedule the next frame
};

await window.WaitForDisposeAsync();

static Color HsvToColor(float h, float s, float v)
{
    h -= MathF.Floor(h); // wrap to [0, 1)
    float c = v * s;
    float hh = h * 6f;
    float x = c * (1f - MathF.Abs(hh % 2f - 1f));
    float r, g, b;
    switch ((int)hh)
    {
        case 0: r = c; g = x; b = 0; break;
        case 1: r = x; g = c; b = 0; break;
        case 2: r = 0; g = c; b = x; break;
        case 3: r = 0; g = x; b = c; break;
        case 4: r = x; g = 0; b = c; break;
        default: r = c; g = 0; b = x; break;
    }
    float m = v - c;
    return new Color(
        (byte)((r + m) * 255f),
        (byte)((g + m) * 255f),
        (byte)((b + m) * 255f));
}

#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/RenderToImage.cs
//
// Renders a 3D scene into a CPU-resident Image and saves it as a BMP
// file -- no window required. Demonstrates the screenshot-shaped use
// case for Image.Render3D: a synchronous "give me a bitmap of this
// scene" call.

using System.Numerics;
using Grape;

var triangle = Mesh.Create([
    new ColorVertex3D(new Vertex3D( 0.0f,  0.5f, 0f), new Color(255, 0,   0)),
    new ColorVertex3D(new Vertex3D( 0.5f, -0.5f, 0f), new Color(0,   255, 0)),
    new ColorVertex3D(new Vertex3D(-0.5f, -0.5f, 0f), new Color(0,   0,   255))
]);

// Use a non-ABGR8888 format on purpose to exercise the per-pixel
// conversion fallback in Image.Render3D. (ABGR8888 takes a memcpy
// fast path; everything else converts pixel by pixel like the
// SkiaSharp bridge does.)
using var image = Image.Create(800, 600, PixelFormat.RGBA8888);

image.Render3D(new Color(0, 0, 32), renderer =>
{
    var aspect = 600f / 800f;
    var transform =
        Matrix4x4.CreateRotationZ(0.4f) *
        Matrix4x4.CreateScale(0.8f) *
        Matrix4x4.CreateScale(aspect, 1f, 1f);

    renderer.DrawMesh(triangle, Shaders.PositionColorWithTransform, transform);
});

var path = Path.Combine(AppContext.BaseDirectory, "RenderToImage.bmp");
image.Save(path);
Console.WriteLine($"Saved: {path}");

#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/RenderToImage.cs
//
// Renders a 3D scene into a CPU-resident Image and saves it as a BMP
// file -- no window required. Demonstrates the screenshot-shaped use
// case for Image.Render3D: a synchronous "give me a bitmap of this
// scene" call.

using System.Numerics;
using Blitter;
using Blitter.Bits;

var triangle = Mesh.Create([
    new ColorVertex3D(new Vertex3D( 0.0f,  0.5f, 0f), Color.Red),
    new ColorVertex3D(new Vertex3D( 0.5f, -0.5f, 0f), Color.Green),
    new ColorVertex3D(new Vertex3D(-0.5f, -0.5f, 0f), Color.Blue)
]);

using var image = Image.Create(800, 600);

image.Render3D(new Color(0, 0, 32), rd =>
{
    var aspect = 600f / 800f;
    var transform = Matrix4x4.CreateRotationZ(0.4f)
        .Scale(0.8f)
        .Scale(aspect, 1f, 1f);

    rd.DrawMesh(triangle, Shaders.PositionColorWithTransform, transform);
});

var path = Path.Combine(AppContext.BaseDirectory, "RenderToImage.bmp");
image.Save(path);
Console.WriteLine($"Saved: {path}");

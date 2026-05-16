#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/RenderToImage.cs

// Renders a 3D scene into a CPU-resident Texture2D and saves it as a .bmp file, no window required.

using System.Numerics;
using Blitter;
using Blitter.Bits;

var triangle = Mesh.Create([
    new ColorVertex3D(new Vertex3D( 0.0f,  0.5f, 0f), Color.Red),
    new ColorVertex3D(new Vertex3D( 0.5f, -0.5f, 0f), Color.Green),
    new ColorVertex3D(new Vertex3D(-0.5f, -0.5f, 0f), Color.Blue)
]);

using var image = Bitmap.Create(800, 600);

image.Render3D(
    new Color(0, 0, 32), 
    rd =>
    {
        var aspect = (float)image.Height / image.Width;
        var transform = Matrix4x4
            .CreateRotationZ(0.4f)
            .Scale(0.8f)
            .Scale(aspect, 1f, 1f);

        rd.DrawMesh(triangle, Shaders.PositionColorWithTransform, transform);
    });

// save in mulitple formats
SaveImage(image, "rendered-image.bmp");
SaveImage(image, "rendered-image.png");
SaveImage(image, "rendered-image.jpg");

static void SaveImage(Bitmap bitmap, string filename)
{
    var path = Path.Combine(AppContext.BaseDirectory, filename);
    bitmap.Save(path);
    Console.WriteLine($"Saved: {path}");
}

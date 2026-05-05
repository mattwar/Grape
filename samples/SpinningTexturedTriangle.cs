#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/SpinningTexturedTriangle.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     ./pack-local.ps1
//
// The samples/NuGet.config in this folder pulls Grape.Graphics from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.

using System.Collections.Immutable;
using System.Numerics;
using Grape;

// A textured triangle. Position is in NDC; UVs are in [0,1] with
// (0,0) at the top-left of the texture and (1,1) at the bottom-right.
var triangle = new Mesh<TextureVertex3D>(
    vertices: ImmutableArray.Create(
        new TextureVertex3D(new Vertex3D( 0.0f,  0.5f, 0f), new Vector2(0.5f, 0f)),
        new TextureVertex3D(new Vertex3D( 0.5f, -0.5f, 0f), new Vector2(1f,   1f)),
        new TextureVertex3D(new Vertex3D(-0.5f, -0.5f, 0f), new Vector2(0f,   1f))),
    indices: ImmutableArray<uint>.Empty);

// Procedurally generate a checkerboard image so any UV/orientation
// mistake is visually obvious.
var checker = CreateCheckerboardImage(256, 256, cellSize: 32);

var window = new Window3D(800, 600)
{
    Title = "Spinning Textured Triangle",
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
    var seconds = (float)frame.ElapsedSinceWindowCreated.TotalSeconds;
    var (width, height) = w.Size;
    var aspect = (float)height / width;
    var transform =
        Matrix4x4.CreateRotationZ(seconds) *
        Matrix4x4.CreateScale(0.8f) *
        Matrix4x4.CreateScale(aspect, 1f, 1f);

    frame.Renderer.RenderTexturedMesh(
        triangle,
        Shaders.TexturedQuadWithMatrix,
        checker,
        transform);

    w.Invalidate(); // schedule the next frame
};

await window.WaitForDisposeAsync();

static Image CreateCheckerboardImage(int width, int height, int cellSize)
{
    var image = Image.Create(width, height, PixelFormat.ABGR8888);
    var dark = new Color(32, 32, 32);
    var light = new Color(220, 220, 220);

    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            var isDark = ((x / cellSize) + (y / cellSize)) % 2 == 0;
            image.SetPixel(x, y, isDark ? dark : light);
        }
    }

    return image;
}

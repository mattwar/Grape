#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/SpinningTexturedTriangle.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// The samples/NuGet.config in this folder pulls Blitter from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.

using System.Numerics;
using Blitter;

// A textured triangle. Position is in NDC; UVs are in [0,1] with
// (0,0) at the top-left of the texture and (1,1) at the bottom-right.
var triangle = Mesh.Create([
    new TextureVertex3D(new Vertex3D( 0.0f,  0.5f, 0f), new Vector2(0.5f, 0f)),
    new TextureVertex3D(new Vertex3D( 0.5f, -0.5f, 0f), new Vector2(1f,   1f)),
    new TextureVertex3D(new Vertex3D(-0.5f, -0.5f, 0f), new Vector2(0f,   1f))
    ]);

// Procedurally generate a checkerboard image so any UV/orientation
// mistake is visually obvious.
var checker = CreateCheckerboardImage(256, 256, cellSize: 32);

var window = new Window3D
{
    Title = "Spinning Textured Triangle",
    BackgroundColor = new Color(0, 0, 32),
    FullScreen = true,
    CloseKey = Key.Escape,
    AutoInvalidate = true,
};

window.Rendering += (w, rd) =>
{
    var seconds = rd.ElapsedSecondsSinceStart;
    var transform =
        Matrix4x4.CreateRotationZ(seconds) *
        Matrix4x4.CreateScale(0.8f) *
        Matrix4x4.CreateScale(1f / rd.AspectRatio, 1f, 1f);

    rd.DrawMesh(
        triangle,
        checker,
        Shaders.PositionTextureWithTransform,
        transform);
};

await window.WaitForCloseAsync();

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

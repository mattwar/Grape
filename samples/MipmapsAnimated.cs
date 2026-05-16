#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/MipmapsAnimated.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// A side-by-side comparison of texture sampling with and without
// mipmaps, isolated from anisotropic filtering.

using System.Numerics;
using Blitter;
using Blitter.Bits;

const int TexSize = 1024;
const int CellSize = 2; // 2-pixel cells = maximum-frequency detail

var noMips   = CreateCheckerboard(TexSize, TexSize, CellSize, mipmaps: false);
var withMips = CreateCheckerboard(TexSize, TexSize, CellSize, mipmaps: true);

const float HalfSize = 0.5f;
var quad = Meshes.TexturedRectangle(size: new Vector2(HalfSize * 2f));

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 0f, 0f),
    Target   = new Vector3(0f, 0f, -1f),
};

var window = new Window3D
{
    Title = "Mipmaps (animated)",
    BackgroundColor = new Color(8, 12, 20),
    FullScreen = true,
    CloseKey = Key.Escape,
};

await window.RunAsync(rd =>
{
    var (width, height) = window.Size;
    var paneWidth = width / 2f;
    var paneAspect = paneWidth / height;
    var viewProjection = camera.GetViewProjection(paneAspect);

    // Distance oscillates smoothly between MinDist (close, no
    // minification) and MaxDist (far, heavy minification),
    // continuously sweeping across the range where alias patterns
    // emerge and shift.
    const float MinDist = 1.5f;
    const float MaxDist = 50f;
    const float Period = 6f; // seconds for a full near->far->near cycle
    var t = rd.ElapsedSecondsSinceStart;
    var phase = (1f - MathF.Cos(t * 2f * MathF.PI / Period)) * 0.5f; // 0..1
    var distance = MinDist + (MaxDist - MinDist) * phase;
    var transform = Matrix4x4.CreateTranslation(0f, 0f, -distance);

    using (rd.PushState())
    {
        rd.Viewport = new Rect(0, 0, paneWidth, height);
        rd.DrawMesh(quad, noMips, Shaders.PositionTextureWithTransform, transform * viewProjection);
        DrawLabel(rd, "No Mipmaps", paneAspect);
    }

    using (rd.PushState())
    {
        rd.Viewport = new Rect(paneWidth, 0, paneWidth, height);
        rd.DrawMesh(quad, withMips, Shaders.PositionTextureWithTransform, transform * viewProjection);
        DrawLabel(rd, "Mipmaps", paneAspect);
    }
});

static void DrawLabel(Renderer3D rd, string text, float paneAspect)
{
    const float Scale = 0.08f;
    var widthInNdc = text.Length * Scale / paneAspect;
    var transform = Matrix4x4
        .CreateScale(Scale / paneAspect, Scale, 1f)
        .Translate(-widthInNdc / 2f, -0.85f, 0f);
    rd.DrawDebugText(text, transform);
}

static Texture2D CreateCheckerboard(int width, int height, int cellSize, bool mipmaps)
{
    var image = Bitmap.Create(width, height, PixelFormat.ABGR8888, mipmaps: mipmaps);
    var dark  = new Color( 30,  30,  30);
    var light = new Color(230, 230, 230);

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

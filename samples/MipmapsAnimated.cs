#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/MipmapsAnimated.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// Side-by-side comparison of texture sampling without and with
// mipmaps, isolated from anisotropic filtering.
//
// The classic "receding floor" demo doesn't isolate mipmaps with
// modern defaults: anisotropic filtering walks the elongated
// screen-pixel footprint along the floor and averages many
// bilinear taps -- killing moire even with no mip chain.
//
// Here a single textured quad faces the camera (perpendicular to
// the view direction) and oscillates toward and away from it.
// Anisotropy can't help because the screen-pixel footprint stays
// square. The only thing that fixes minification aliasing is a
// real mip chain.
//
// LEFT pane:  Image without mipmaps. As the quad recedes, the
//             bilinear sampler picks essentially-arbitrary texels
//             per screen pixel -> wandering moire patterns,
//             shimmering, "swimming" content.
//
// RIGHT pane: Same texture content, `mipmaps: true`. The renderer
//             generates a full mip chain and the sampler picks
//             the level matching the screen-pixel footprint at
//             each instant -> stays a clean uniform gray as the
//             quad shrinks.

using System.Collections.Immutable;
using System.Numerics;
using Grape;

const int TexSize = 1024;
const int CellSize = 2; // 2-pixel cells = maximum-frequency detail

var noMips   = CreateCheckerboard(TexSize, TexSize, CellSize, mipmaps: false);
var withMips = CreateCheckerboard(TexSize, TexSize, CellSize, mipmaps: true);

const float HalfSize = 0.5f;
var quad = new Mesh<TextureVertex3D>(
    vertices: ImmutableArray.Create(
        new TextureVertex3D(new Vertex3D(-HalfSize, -HalfSize, 0f), new Vector2(0f, 1f)),
        new TextureVertex3D(new Vertex3D( HalfSize, -HalfSize, 0f), new Vector2(1f, 1f)),
        new TextureVertex3D(new Vertex3D( HalfSize,  HalfSize, 0f), new Vector2(1f, 0f)),
        new TextureVertex3D(new Vertex3D(-HalfSize,  HalfSize, 0f), new Vector2(0f, 0f))),
    indices: ImmutableArray.Create<uint>(0, 1, 2, 0, 2, 3));

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

window.Rendering += (w, rd) =>
{
    var (width, height) = w.Size;
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
    var t = (float)rd.ElapsedSinceStart.TotalSeconds;
    var phase = (1f - MathF.Cos(t * 2f * MathF.PI / Period)) * 0.5f; // 0..1
    var distance = MinDist + (MaxDist - MinDist) * phase;
    var transform = Matrix4x4.CreateTranslation(0f, 0f, -distance);

    using (rd.PushState())
    {
        rd.Viewport = new Rect(0, 0, paneWidth, height);
        rd.DrawMesh(quad, noMips, ShaderSets.PositionTextureWithTransform, transform * viewProjection);
        DrawLabel(rd, "No Mipmaps", paneAspect);
    }

    using (rd.PushState())
    {
        rd.Viewport = new Rect(paneWidth, 0, paneWidth, height);
        rd.DrawMesh(quad, withMips, ShaderSets.PositionTextureWithTransform, transform * viewProjection);
        DrawLabel(rd, "Mipmaps", paneAspect);
    }

    w.Invalidate();
};

await window.WaitForCloseAsync();

static void DrawLabel(Renderer3D rd, string text, float paneAspect)
{
    // Debug text is drawn in NDC: each character is 1x1 unit. Center
    // horizontally near the bottom of the pane. The X scale is divided
    // by the pane aspect so characters stay square in a half-screen
    // viewport rather than being stretched horizontally.
    const float Scale = 0.08f;
    var widthInNdc = text.Length * Scale / paneAspect;
    var transform =
        Matrix4x4.CreateScale(Scale / paneAspect, Scale, 1f) *
        Matrix4x4.CreateTranslation(-widthInNdc / 2f, -0.85f, 0f);
    rd.DrawDebugText(text, transform);
}

static Image CreateCheckerboard(int width, int height, int cellSize, bool mipmaps)
{
    var image = Image.Create(width, height, PixelFormat.ABGR8888, mipmaps: mipmaps);
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

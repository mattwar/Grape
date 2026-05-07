#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/MipmappedFloor.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// The samples/NuGet.config in this folder pulls Grape.Graphics from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.
//
// Side-by-side comparison of texture sampling without and with
// mipmaps. Two large quads recede toward the horizon; both sample the
// same procedurally-generated high-frequency checkerboard texture.
//
// LEFT pane:  Image created without mipmaps. The bilinear sampler
//             picks one of millions of texel candidates per screen
//             pixel at distance, producing aliasing / shimmer /
//             moire patterns -- the visual signature of undersampling.
//
// RIGHT pane: Same texture content, but the Image was created with
//             `mipmaps: true`. The renderer allocated a full mip
//             chain and runs SDL_GenerateMipmapsForGPUTexture after
//             the base level uploads. The sampler picks the level
//             whose texels match the screen-pixel footprint and
//             trilinearly blends adjacent levels for smooth
//             transitions.

using System.Collections.Immutable;
using System.Numerics;
using Grape;

const int TexSize = 512;
const int CellSize = 8; // small cells = lots of high-frequency detail

// Two images with identical pixel content; the only difference is
// the Mipmaps flag, which the renderer reads at GPU upload time.
var noMips    = CreateCheckerboard(TexSize, TexSize, CellSize, mipmaps: false);
var withMips  = CreateCheckerboard(TexSize, TexSize, CellSize, mipmaps: true);

// A single quad in the XZ plane (Y = 0), tiled 8x by repeating UVs
// to cover a long stretch of receding ground. The wrap-around UV
// repeats across the surface and pushes the texture deep into
// minified territory near the horizon.
const float HalfExtent = 8f;
const float UvRepeat   = 8f;
var floor = new Mesh<TextureVertex3D>(
    vertices: ImmutableArray.Create(
        new TextureVertex3D(new Vertex3D(-HalfExtent, 0f, -HalfExtent), new Vector2(0f,        0f)),
        new TextureVertex3D(new Vertex3D( HalfExtent, 0f, -HalfExtent), new Vector2(UvRepeat,  0f)),
        new TextureVertex3D(new Vertex3D( HalfExtent, 0f,  HalfExtent), new Vector2(UvRepeat,  UvRepeat)),
        new TextureVertex3D(new Vertex3D(-HalfExtent, 0f,  HalfExtent), new Vector2(0f,        UvRepeat))),
    indices: ImmutableArray.Create<uint>(0, 1, 2, 0, 2, 3));

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 0.4f, 0.5f),
    Target   = new Vector3(0f, 0f, -HalfExtent),
};

var window = new Window3D
{
    Title = "Mipmapped Floor",
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

    // Left pane: no mipmaps -> bilinear sampling at minification ->
    // shimmering / moire near the horizon.
    using (rd.PushState())
    {
        rd.Viewport = new Rect(0, 0, paneWidth, height);
        rd.DrawMesh(floor, noMips, Shaders.PositionTextureWithTransform, viewProjection);
    }

    // Right pane: mipmaps generated -> trilinear sampling picks the
    // right level for each screen pixel -> clean, stable falloff.
    using (rd.PushState())
    {
        rd.Viewport = new Rect(paneWidth, 0, paneWidth, height);
        rd.DrawMesh(floor, withMips, Shaders.PositionTextureWithTransform, viewProjection);
    }

    w.Invalidate();
};

await window.WaitForCloseAsync();

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

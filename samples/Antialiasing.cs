#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/Antialiasing.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// Demonstrates Renderer3D.Antialiasing (MSAA). Press SPACE to cycle
// the level: None -> X2 -> X4 -> X8 -> None ...
//
// To make the difference obvious on high-DPI displays, this sample
// goes out of its way to maximise visible aliasing:
//
//   * Thin spoke triangles. Long, narrow triangles are the worst
//     case for jaggies because each pixel sits very close to two
//     edges at once.
//   * Slow rotation. Static jaggies are easy to overlook; moving
//     ones "crawl" and shimmer, which the eye picks up immediately.
//   * Small render viewport. The fan is rendered into a 400x400
//     pixel viewport in the middle of the screen, then the result
//     is presented at native resolution. This effectively makes
//     each rendered pixel huge, exaggerating staircase patterns.
//
// In a real app, MSAA is set once at startup and forgotten:
//
//     window.Rendering += (w, rd) => { rd.Antialiasing = Antialiasing.X4; ... };

using System.Collections.Immutable;
using System.Numerics;
using Grape;

const int Spokes = 16;
const float InnerRadius = 0.05f;
const float OuterRadius = 1.0f;
const float HalfThickness = 0.015f; // very thin -> very aliased

var verts = ImmutableArray.CreateBuilder<ColorVertex3D>();
var idxs = ImmutableArray.CreateBuilder<uint>();
for (int i = 0; i < Spokes; i++)
{
    var angle = i * MathF.Tau / Spokes;
    var dirX = MathF.Cos(angle);
    var dirY = MathF.Sin(angle);
    var perpX = -dirY * HalfThickness;
    var perpY =  dirX * HalfThickness;
    var color = Color.FromHsv(i * 360f / Spokes, 0.6f, 1f);

    var baseIdx = (uint)verts.Count;
    verts.Add(new ColorVertex3D(new Vertex3D(dirX * InnerRadius - perpX, dirY * InnerRadius - perpY, 0f), color));
    verts.Add(new ColorVertex3D(new Vertex3D(dirX * InnerRadius + perpX, dirY * InnerRadius + perpY, 0f), color));
    verts.Add(new ColorVertex3D(new Vertex3D(dirX * OuterRadius - perpX, dirY * OuterRadius - perpY, 0f), color));
    verts.Add(new ColorVertex3D(new Vertex3D(dirX * OuterRadius + perpX, dirY * OuterRadius + perpY, 0f), color));
    idxs.Add(baseIdx + 0); idxs.Add(baseIdx + 2); idxs.Add(baseIdx + 1);
    idxs.Add(baseIdx + 1); idxs.Add(baseIdx + 2); idxs.Add(baseIdx + 3);
}
var fan = Mesh.Create<ColorVertex3D>(verts.ToImmutable().AsSpan(), idxs.ToImmutable().AsSpan());

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 0f, 2.4f),
};

var window = new Window3D
{
    Title = "Antialiasing (press SPACE to cycle)",
    BackgroundColor = new Color(8, 8, 24),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var levels = new[] { Antialiasing.None, Antialiasing.X2, Antialiasing.X4, Antialiasing.X8 };
int levelIndex = 0;

window.KeyDown += (_, e) =>
{
    if (e.Key == Key.Space)
        levelIndex = (levelIndex + 1) % levels.Length;
};

window.Rendering += (w, rd) =>
{
    rd.Antialiasing = levels[levelIndex];

    var t = (float)rd.ElapsedSinceStart.TotalSeconds;
    var (width, height) = w.Size;

    // Render the fan into a small square viewport in the middle of
    // the screen. Smaller render area -> bigger effective pixels ->
    // jaggies are unmistakable even at high DPI.
    const float ViewportSize = 400f;
    var viewportRect = new Rect(
        (width  - ViewportSize) / 2f,
        (height - ViewportSize) / 2f,
        ViewportSize,
        ViewportSize);

    var viewProjection = camera.GetViewProjection(1f); // square viewport
    var model = Matrix4x4.CreateRotationZ(t * 0.3f);

    using (rd.PushState())
    {
        rd.Viewport = viewportRect;
        rd.DrawMesh(fan, Shaders.PositionColorWithTransform, model * viewProjection);
    }

    // Label uses the full window viewport so the text isn't squished.
    var labelVp = camera.GetViewProjection((float)width / height);
    DrawLabel(rd, $"Antialiasing: {levels[levelIndex]}  (SPACE to cycle)",
        yOffset: -1.7f, labelVp);

    w.Invalidate();
};

static void DrawLabel(Renderer3D renderer, string text, float yOffset, Matrix4x4 viewProjection)
{
    const float scale = 0.06f;
    var transform =
        Matrix4x4.CreateTranslation(-text.Length / 2f, 0f, 0f) *
        Matrix4x4.CreateScale(scale) *
        Matrix4x4.CreateTranslation(0f, yOffset, 0f) *
        viewProjection;
    renderer.DrawDebugText(text, transform);
}

await window.WaitForCloseAsync();

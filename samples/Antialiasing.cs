#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/Antialiasing.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// Demonstrates Renderer3D.Antialiasing (MSAA). Press SPACE to cycle:
// None -> X2 -> X4 -> X8 -> None ...
//
// The scene is a wagon-wheel of 64 razor-thin spokes, slowly rotating.
// Thin diagonal edges are the worst case for aliasing -- a single-pixel
// wide line tilted off-axis produces a crawling staircase as it rotates.
// MSAA replaces the staircase with a smooth gradient.
//
// At 4K the per-pixel jaggies are tiny but the *crawling* is what your
// eye picks up: with None the spokes shimmer and twitch as they spin;
// with X4 or X8 the rotation looks fluid and the spokes hold steady.

using System.Collections.Immutable;
using System.Numerics;
using Grape;

const int Spokes = 64;
const float HalfThickness = 0.003f; // ~1-2 pixels wide at 4K
const float InnerRadius = 0.05f;
const float OuterRadius = 0.95f;

var verts = ImmutableArray.CreateBuilder<ColorVertex3D>();
var idxs = ImmutableArray.CreateBuilder<uint>();
for (int i = 0; i < Spokes; i++)
{
    float a = i * MathF.Tau / Spokes;
    var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
    var perp = new Vector2(-dir.Y, dir.X) * HalfThickness;
    var inner = dir * InnerRadius;
    var outer = dir * OuterRadius;
    var color = Color.FromHsv(i * 360f / Spokes, 0.7f, 1f);
    uint baseIdx = (uint)verts.Count;
    verts.Add(new ColorVertex3D(new Vertex3D(inner.X - perp.X, inner.Y - perp.Y, 0f), color));
    verts.Add(new ColorVertex3D(new Vertex3D(inner.X + perp.X, inner.Y + perp.Y, 0f), color));
    verts.Add(new ColorVertex3D(new Vertex3D(outer.X + perp.X, outer.Y + perp.Y, 0f), color));
    verts.Add(new ColorVertex3D(new Vertex3D(outer.X - perp.X, outer.Y - perp.Y, 0f), color));
    idxs.Add(baseIdx); idxs.Add(baseIdx + 1); idxs.Add(baseIdx + 2);
    idxs.Add(baseIdx); idxs.Add(baseIdx + 2); idxs.Add(baseIdx + 3);
}

var wheel = Mesh.Create<ColorVertex3D>(verts.ToImmutable().AsSpan(), idxs.ToImmutable().AsSpan());

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
    float aspect = (float)width / height;

    // Slow rotation -- aliasing is most visible when edges crawl
    // through pixel boundaries gradually.
    var model = Matrix4x4.CreateRotationZ(t * 0.1f);
    var viewProjection = camera.GetViewProjection(aspect);

    rd.DrawMesh(wheel, Shaders.PositionColorWithTransform, model * viewProjection);

    DrawLabel(rd, $"Antialiasing: {levels[levelIndex]}", yOffset: -0.85f, viewProjection);
    DrawLabel(rd, "Press SPACE to cycle (None / X2 / X4 / X8)", yOffset: -0.95f, viewProjection);

    w.Invalidate();
};

static void DrawLabel(Renderer3D renderer, string text, float yOffset, Matrix4x4 viewProjection)
{
    const float scale = 0.04f;
    var transform =
        Matrix4x4.CreateTranslation(-text.Length / 2f, 0f, 0f) *
        Matrix4x4.CreateScale(scale) *
        Matrix4x4.CreateTranslation(0f, yOffset, 0f) *
        viewProjection;
    renderer.DrawDebugText(text, transform);
}

await window.WaitForCloseAsync();

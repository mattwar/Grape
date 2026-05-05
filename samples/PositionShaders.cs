#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/PositionShaders.cs
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

// Exercises every position-only built-in shader in `Shaders`:
//   - Shaders.Position                (white, no transform)
//   - Shaders.PositionTransform       (white, transformed)
//   - Shaders.PositionTransformColor  (per-draw fragment color, transformed)
// All three draw the same triangle mesh in different quadrants of the window
// so the visual result tells you at a glance whether each shader pipeline
// survives the runtime HLSL -> shadercross path.

// Position-only triangle, ~1 unit tall, centered at origin.
var triangle = new Mesh<Vertex3D>(
    vertices: ImmutableArray.Create(
        new Vertex3D( 0.0f,  0.5f, 0f),
        new Vertex3D( 0.5f, -0.5f, 0f),
        new Vertex3D(-0.5f, -0.5f, 0f)),
    indices: ImmutableArray<uint>.Empty);

// A static triangle whose positions are already in NDC inside the top-left
// quadrant. Used to exercise Shaders.Position, which takes no transform.
var staticTopLeft = new Mesh<Vertex3D>(
    vertices: ImmutableArray.Create(
        new Vertex3D(-0.5f,  0.7f, 0f),
        new Vertex3D(-0.3f,  0.3f, 0f),
        new Vertex3D(-0.7f,  0.3f, 0f)),
    indices: ImmutableArray<uint>.Empty);

var window = new Window3D(800, 600)
{
    Title = "Position Shaders",
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

    var fit  = Matrix4x4.CreateScale(0.35f) * Matrix4x4.CreateScale(aspect, 1f, 1f);
    var spin = Matrix4x4.CreateRotationZ(seconds);

    // Top-left: Shaders.Position (no transform).
    frame.Renderer.RenderMesh(staticTopLeft, Shaders.Position);

    // Top-right: Shaders.PositionTransform (white, spinning, translated).
    var topRight = spin * fit * Matrix4x4.CreateTranslation(0.5f, 0.5f, 0f);
    frame.Renderer.RenderMesh(triangle, Shaders.PositionTransform, topRight);

    // Bottom-left: Shaders.PositionTransformColor with red.
    var bottomLeft = spin * fit * Matrix4x4.CreateTranslation(-0.5f, -0.5f, 0f);
    frame.Renderer.RenderMesh(triangle, Shaders.PositionTransformColor, new PositionTransformColorArgs
    {
        Mvp = bottomLeft,
        Color = new Vector4(1f, 0.2f, 0.2f, 1f),
    });

    // Bottom-right: Shaders.PositionTransformColor with hue-cycling color
    // and counter-rotation.
    var bottomRight =
        Matrix4x4.CreateRotationZ(-seconds) * fit *
        Matrix4x4.CreateTranslation(0.5f, -0.5f, 0f);
    frame.Renderer.RenderMesh(triangle, Shaders.PositionTransformColor, new PositionTransformColorArgs
    {
        Mvp = bottomRight,
        Color = HueToRgb(seconds * 0.25f),
    });

    w.Invalidate(); // schedule the next frame
};

await window.WaitForDisposeAsync();

static Vector4 HueToRgb(float hue)
{
    hue -= MathF.Floor(hue);
    var h6 = hue * 6f;
    var x  = 1f - MathF.Abs((h6 % 2f) - 1f);
    var (r, g, b) = (int)h6 switch
    {
        0 => (1f, x,  0f),
        1 => (x,  1f, 0f),
        2 => (0f, 1f, x),
        3 => (0f, x,  1f),
        4 => (x,  0f, 1f),
        _ => (1f, 0f, x),
    };
    return new Vector4(r, g, b, 1f);
}

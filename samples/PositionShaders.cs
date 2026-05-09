#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/PositionShaders.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// The samples/NuGet.config in this folder pulls Blitter from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.

using System.Numerics;
using Blitter;

// Exercises every position-only built-in shader in `Shaders`:
//   - ShaderSets.Position                       (white, no transform)
//   - ShaderSets.PositionWithTransform          (white, transformed)
//   - ShaderSets.PositionWithTransformAndColor  (per-draw fragment color, transformed)
// All three draw the same triangle mesh in different quadrants of the window
// so the visual result tells you at a glance whether each shader pipeline
// survives the runtime HLSL -> shadercross path.

// Position-only triangle, ~1 unit tall, centered at origin.
var triangle = Mesh.Create([
    new Vertex3D( 0.0f,  0.5f, 0f),
    new Vertex3D( 0.5f, -0.5f, 0f),
    new Vertex3D(-0.5f, -0.5f, 0f)
    ]);

// A static triangle whose positions are already in NDC inside the top-left
// quadrant. Used to exercise ShaderSets.Position, which takes no transform.
var staticTopLeft = Mesh.Create([
    new Vertex3D(-0.5f,  0.7f, 0f),
    new Vertex3D(-0.3f,  0.3f, 0f),
    new Vertex3D(-0.7f,  0.3f, 0f)
    ]);

var window = new Window3D
{
    Title = "Position Shaders",
    BackgroundColor = new Color(0, 0, 32),
    FullScreen = true,
    CloseKey = Key.Escape,
};

window.Rendering += (w, rd) =>
{
    var seconds = (float)rd.ElapsedSinceStart.TotalSeconds;
    var (width, height) = w.Size;
    var aspect = (float)height / width;

    var fit  = Matrix4x4.CreateScale(0.35f) * Matrix4x4.CreateScale(aspect, 1f, 1f);
    var spin = Matrix4x4.CreateRotationZ(seconds);

    // Top-left: ShaderSets.Position (no transform).
    rd.DrawMesh(staticTopLeft, ShaderSets.Position);

    // Top-right: ShaderSets.PositionWithTransform (white, spinning, translated).
    var topRight = spin * fit * Matrix4x4.CreateTranslation(0.5f, 0.5f, 0f);
    rd.DrawMesh(triangle, ShaderSets.PositionWithTransform, topRight);

    // Bottom-left: ShaderSets.PositionWithTransformAndColor with red.
    var bottomLeft = spin * fit * Matrix4x4.CreateTranslation(-0.5f, -0.5f, 0f);
    rd.DrawMesh(triangle, ShaderSets.PositionWithTransformAndColor, new TransformAndFColorArgs
    {
        Transform = bottomLeft,
        FColor = new Vector4(1f, 0.2f, 0.2f, 1f),
    });

    // Bottom-right: ShaderSets.PositionWithTransformAndColor with hue-cycling color
    // and counter-rotation.
    var bottomRight =
        Matrix4x4.CreateRotationZ(-seconds) * fit *
        Matrix4x4.CreateTranslation(0.5f, -0.5f, 0f);

    rd.DrawMesh(triangle, ShaderSets.PositionWithTransformAndColor, new TransformAndFColorArgs
    {
        Transform = bottomRight,
        FColor = HueToRgb(seconds * 0.25f),
    });

    w.Invalidate(); // schedule the next frame
};

await window.WaitForCloseAsync();

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

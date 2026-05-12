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
using Blitter.Bits;

// Exercises every position-only built-in shader in `Shaders`:
//   - Shaders.Position                       (white, no transform)
//   - Shaders.PositionWithTransform          (white, transformed)
//   - Shaders.PositionWithTransformAndColor  (per-draw fragment color, transformed)
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
// quadrant. Used to exercise Shaders.Position, which takes no transform.
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

await window.RunAsync(rd =>
{
    var seconds = rd.ElapsedSecondsSinceStart;
    var aspect = 1f / rd.AspectRatio; // height / width

    var fit  = Matrix4x4.CreateScale(0.35f).Scale(aspect, 1f, 1f);
    var spin = Matrix4x4.CreateRotationZ(seconds);

    // Top-left: Shaders.Position (no transform).
    rd.DrawMesh(staticTopLeft, Shaders.Position);

    // Top-right: Shaders.PositionWithTransform (white, spinning, translated).
    var topRight = (spin * fit).Translate(0.5f, 0.5f, 0f);
    rd.DrawMesh(triangle, Shaders.PositionWithTransform, topRight);

    // Bottom-left: Shaders.PositionWithTransformAndColor with red.
    var bottomLeft = (spin * fit).Translate(-0.5f, -0.5f, 0f);
    rd.DrawMesh(triangle, Shaders.PositionWithTransformAndColor, new TransformAndFColorArgs
    {
        Transform = bottomLeft,
        FColor = new Vector4(1f, 0.2f, 0.2f, 1f),
    });

    // Bottom-right: Shaders.PositionWithTransformAndColor with hue-cycling color
    // and counter-rotation.
    var bottomRight = (Matrix4x4.CreateRotationZ(-seconds) * fit)
        .Translate(0.5f, -0.5f, 0f);

    rd.DrawMesh(triangle, Shaders.PositionWithTransformAndColor, new TransformAndFColorArgs
    {
        Transform = bottomRight,
        FColor = HueToRgb(seconds * 0.25f),
    });
});static Vector4 HueToRgb(float hue)
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

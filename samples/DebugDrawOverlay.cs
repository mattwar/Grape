#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/DebugDrawOverlay.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// DebugDraw is a global ad-hoc overlay: any code, anywhere, can call
// DebugDraw.DrawLine/DrawBox/DrawSphere/DrawAxes and the primitives
// appear on top of the scene -- as long as some renderer has opted in
// by setting `DebugDrawEnabled = true`. Calls are no-ops when no
// renderer is consuming, so leaving them in release code is cheap.
//
// This sample draws a single textured cube as the "real" scene, then
// uses DebugDraw from the render handler to overlay:
//
//   * world-space axes at the origin
//   * a wireframe sphere orbiting the cube
//   * a tight wireframe box around the cube (its bounds gizmo)
//   * a moving line tracing the orbit path

using System.Numerics;
using Blitter;
using Blitter.Bits;

var cube = Meshes.Cube(new Color(180, 110, 80), size: new Vector3(1f));

var window = new Window3D
{
    Title = "DebugDraw overlay -- toggle with F3",
    BackgroundColor = new Color(12, 14, 24),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(2.5f, 2.0f, 4.5f),
    Target = Vector3.Zero,
};

window.Renderer.Camera = camera;
window.Renderer.AmbientLight = new Color(40, 40, 60);
window.Renderer.DirectionalLight = new DirectionalLight(
    Vector3.Normalize(new Vector3(-0.4f, -1f, -0.6f)),
    Color.White);

// Opt this renderer in as the DebugDraw consumer. Without this,
// every DebugDraw.* call below is a no-op.
window.Renderer.DebugDrawEnabled = true;

// F3 toggles the overlay on/off at runtime.
window.KeyDown += (_, e) =>
{
    if (e.Key == Key.F3 && !e.IsRepeat)
        window.Renderer.DebugDrawEnabled = !window.Renderer.DebugDrawEnabled;
};

window.Rendering += (w, rd) =>
{
    var t = (float)rd.ElapsedSinceStart.TotalSeconds;

    // The "real" scene: one lit cube.
    rd.DrawMesh(cube, ShaderSets.LitColor, new LitArgs(Matrix4x4.CreateRotationY(t * 0.5f)));

    // ---- Debug overlay --------------------------------------------

    // World axes at origin.
    DebugDraw.DrawAxes(Vector3.Zero, length: 1.5f);

    // Bounds gizmo around the (rotating) cube. Use the AABB of the
    // unrotated cube as a stand-in -- good enough for "where is it."
    DebugDraw.DrawBoxCentered(Vector3.Zero, new Vector3(1.2f), Color.Yellow);

    // Wireframe sphere orbiting the cube.
    var orbit = new Vector3(
        MathF.Cos(t) * 2.0f,
        0.6f + 0.3f * MathF.Sin(t * 2f),
        MathF.Sin(t) * 2.0f);
    DebugDraw.DrawSphere(orbit, radius: 0.25f, Color.Cyan);

    // Line from origin to the orbiting sphere -- shows DebugDraw can
    // be sprinkled freely in update/render code.
    DebugDraw.DrawLine(Vector3.Zero, orbit, Color.Magenta);

    w.Invalidate();
};

await window.WaitForCloseAsync();

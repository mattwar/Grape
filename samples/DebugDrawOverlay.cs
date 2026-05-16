#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/DebugDrawOverlay.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//

// Demonstrates DebugDraw: a simple API for drawing a debug overlay

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

// Enable DebugDraw to draw to this window
window.Renderer.DebugDrawEnabled = true;

await window.RunAsync(rd =>
{
    var t = rd.ElapsedSecondsSinceStart;

    // toggle the debug drawing
    if (window.Input.WasJustPressed(Key.Space))
        window.Renderer.DebugDrawEnabled = !window.Renderer.DebugDrawEnabled;
 
    // The "real" scene: one lit cube.
    rd.DrawMesh(cube, Shaders.LitColor, new LitArgs(Matrix4x4.CreateRotationY(t * 0.5f)));

    // ---- Debug overlay --------------------------------------------

    // World axes at origin.
    DebugDraw.DrawAxes(Vector3.Zero, length: 1.5f);

    // Bounds gizmo around the (rotating) cube. Use the AABB of the
    // unrotated cube as a stand-in -- good enough for "where is it."
    DebugDraw.DrawBoxCentered(Vector3.Zero, new Vector3(1.2f), Color.Yellow);

    // Wireframe sphere orbiting the cube.
    var orbit = MathG.Orbit(t, radius: 2f) + Vector3.UnitY * (0.6f + 0.3f * MathF.Sin(t * 2f));
    DebugDraw.DrawSphere(orbit, radius: 0.25f, Color.Cyan);

    // Line from origin to the orbiting sphere -- shows DebugDraw can
    // be sprinkled freely in update/render code.
    DebugDraw.DrawLine(Vector3.Zero, orbit, Color.Magenta);

    // Billboarded label that tracks the orbiting sphere. Hidden when
    // the anchor is behind the camera.
    DebugDraw.DrawText3D(orbit, $"orbit t={t,5:F1}", offsetY: -28f);
    DebugDraw.DrawText3D(Vector3.Zero, "origin", offsetY: -28f);

    // Screen-space text. Uses an interpolated string handler so when
    // DebugDraw.IsActive is false (e.g. F3 toggled it off), the
    // formatting work is skipped entirely -- no allocation, no boxing.
    var fps = 1.0 / Math.Max(rd.ElapsedSinceLastRender.TotalSeconds, 1e-6);
    DebugDraw.DrawText($"fps {fps,5:F1}", 12, 12);
    DebugDraw.DrawText($"orbit ({orbit.X,6:F2}, {orbit.Y,6:F2}, {orbit.Z,6:F2})", 12, 40);
    DebugDraw.DrawText("Press SPACE to toggle DebugDraw", 12, 68);
});
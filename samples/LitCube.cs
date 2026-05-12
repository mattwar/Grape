#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/LitCube.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Demonstrates Blitter's directional + ambient lighting. The cube uses
// per-face normals (so each face is uniformly shaded by its angle to
// the light) and a single flat color. As the cube rotates, faces
// brighten and darken under the directional light, with the ambient
// term keeping the dark side from going pitch black.
//
// Lighting is composed automatically: the renderer's Camera,
// AmbientLight, and DirectionalLight properties feed the LitColor
// shader through the IUniformArgs trait mechanism, so the per-draw
// arguments only carry the user-supplied model matrix.

using System.Numerics;
using Blitter;
using Blitter.Bits;

// Lit cube with per-face normals so each face shades uniformly under
// the directional light. `Meshes.Cube` returns 24 vertices (4 per face)
// with the right normals already baked in.
var cube = Meshes.Cube(new Color(220, 100, 80), size: new Vector3(2f));

var window = new Window3D
{
    Title = "Lit cube: directional + ambient",
    BackgroundColor = new Color(8, 8, 24),
    FullScreen = true,
    CloseKey = Key.Escape,
    AutoAnimate = true,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 1.5f, 5f),
};

window.Rendering += (w, rd) =>
{
    rd.Camera = camera;

    // Soft ambient so the unlit side isn't pure black; bright
    // directional that orbits the cube so every face gets a turn.
    rd.AmbientLight = new Color(40, 40, 60);

    var t = rd.ElapsedSecondsSinceStart;
    var lightDir = Vector3.Normalize(MathG.Orbit(t, speed: 0.5f) + Vector3.UnitY * 0.6f);
    rd.DirectionalLight = new DirectionalLight(lightDir, Color.White);

    var model =
        Matrix4x4.CreateRotationY(t * 0.6f) *
        Matrix4x4.CreateRotationX(t * 0.3f);

    using (rd.PushState())
    {
        rd.CullMode = CullMode.Back;
        rd.DrawMesh(cube, Shaders.LitColor, new LitArgs(model));
    }
};

await window.WaitForCloseAsync();

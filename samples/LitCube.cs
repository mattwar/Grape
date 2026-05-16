#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/LitCube.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// Demonstrates Blitter's directional + ambient lighting.

using System.Numerics;
using Blitter;
using Blitter.Bits;

var cube = Meshes.Cube(new Color(220, 100, 80), size: new Vector3(2f));

var window = new Window3D
{
    Title = "Lit cube: directional + ambient",
    BackgroundColor = new Color(8, 8, 24),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 1.5f, 5f),
};

await window.RunAsync(rd =>
{
    rd.Camera = camera;

    // Soft ambient so the unlit side isn't pure black
    rd.AmbientLight = new Color(40, 40, 60);

    // bright directional that orbits the cube so every face gets a turn.
    var t = rd.ElapsedSecondsSinceStart;
    var lightDir = Vector3.Normalize(MathG.Orbit(t, speed: 0.5f) + Vector3.UnitY * 0.6f);
    rd.DirectionalLight = new DirectionalLight(lightDir, Color.White);

    var model = Matrix4x4.CreateRotationY(t * 0.6f).RotateX(t * 0.3f);

    using (rd.PushState())
    {
        rd.CullMode = CullMode.Back;
        rd.DrawMesh(cube, Shaders.LitColor, new LitArgs(model));
    }
});
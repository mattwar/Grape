#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/OrbitingLight.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// A small spinning cube sits at the origin while a bright marker cube
// orbits around it, representing the directional light's position. The
// big cube's lit faces always face the marker -- as the marker swings
// around, you can watch the bright spot crawl across the cube.
//
// (A directional light is technically infinitely far away with no
// position, just a direction. The marker visualises that direction by
// sitting on a small orbit ring; the light direction we feed the
// renderer is always "from cube toward marker".)

using System.Numerics;
using Blitter;
using Blitter.Bits;

// Big lit cube: per-face normals so each face shades uniformly under
// the directional light.
var bigCube = Meshes.Cube(new Color(220, 100, 80), size: new Vector3(2f));

// Small unlit white marker cube to show the light's position. We keep
// this hand-built (rather than using Meshes.Cube) because the marker
// is intentionally rendered with the unlit PositionColor shader so it
// glows uniformly white regardless of the light's direction.
var markerVerts = new ColorVertex3D[]
{
    new(new Vertex3D(-1f, -1f, -1f), Color.White),
    new(new Vertex3D( 1f, -1f, -1f), Color.White),
    new(new Vertex3D( 1f,  1f, -1f), Color.White),
    new(new Vertex3D(-1f,  1f, -1f), Color.White),
    new(new Vertex3D(-1f, -1f,  1f), Color.White),
    new(new Vertex3D( 1f, -1f,  1f), Color.White),
    new(new Vertex3D( 1f,  1f,  1f), Color.White),
    new(new Vertex3D(-1f,  1f,  1f), Color.White),
};
var markerIdx = new uint[]
{
    4, 5, 6,   4, 6, 7,   1, 0, 3,   1, 3, 2,
    0, 4, 7,   0, 7, 3,   5, 1, 2,   5, 2, 6,
    7, 6, 2,   7, 2, 3,   0, 1, 5,   0, 5, 4,
};
var marker = Mesh.Create(markerVerts, markerIdx);

var window = new Window3D
{
    Title = "Orbiting light around a small lit cube",
    BackgroundColor = new Color(8, 8, 24),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 1f, 4f),
};

window.Rendering += (w, rd) =>
{
    rd.Camera = camera;
    rd.AmbientLight = new Color(30, 30, 50);

    var t = (float)rd.ElapsedSinceStart.TotalSeconds;

    // The light "lives" at this orbit position. Direction to feed the
    // shader is from the cube (origin) toward this point, so the lit
    // face is the one pointing at the marker.
    const float orbitRadius = 2.5f;
    const float orbitSpeed  = 0.6f;
    var lightPos = new Vector3(
        MathF.Cos(t * orbitSpeed) * orbitRadius,
        MathF.Sin(t * 0.4f) * 0.8f + 1.2f,
        MathF.Sin(t * orbitSpeed) * orbitRadius);
    rd.DirectionalLight = new DirectionalLight(
        Vector3.Normalize(lightPos),
        Color.White);

    // Big cube: small (~0.6 unit half-extent) and gently spinning.
    var bigModel =
        Matrix4x4.CreateScale(0.6f) *
        Matrix4x4.CreateRotationY(t * 0.5f) *
        Matrix4x4.CreateRotationX(t * 0.25f);

    using (rd.PushState())
    {
        rd.CullMode = CullMode.Back;
        rd.DrawMesh(bigCube, ShaderSets.LitColor, new LitArgs(bigModel));
    }

    // Marker: small unlit white cube parked at the light's orbit
    // position. Uses the unlit transform shader -- it ignores ambient
    // and directional state entirely.
    var markerModel =
        Matrix4x4.CreateScale(0.08f) *
        Matrix4x4.CreateTranslation(lightPos);

    using (rd.PushState())
    {
        rd.CullMode = CullMode.Back;
        rd.DrawMesh(marker, ShaderSets.PositionColorWithTransform, markerModel);
    }

    w.Invalidate();
};

await window.WaitForCloseAsync();

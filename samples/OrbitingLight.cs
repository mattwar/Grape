#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/OrbitingLight.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
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
using Grape;

// ---- Big cube: per-face normals, flat coloured faces ---------------------
var faces = new (Vector3 Normal, Color Color, Vector3 A, Vector3 B, Vector3 C, Vector3 D)[]
{
    (new( 1, 0, 0), new Color(220,  60,  60),
        new( 1,-1,-1), new( 1, 1,-1), new( 1, 1, 1), new( 1,-1, 1)),
    (new(-1, 0, 0), new Color( 60, 200, 220),
        new(-1,-1, 1), new(-1, 1, 1), new(-1, 1,-1), new(-1,-1,-1)),
    (new( 0, 1, 0), new Color( 80, 200,  80),
        new(-1, 1, 1), new( 1, 1, 1), new( 1, 1,-1), new(-1, 1,-1)),
    (new( 0,-1, 0), new Color(220, 200,  60),
        new(-1,-1,-1), new( 1,-1,-1), new( 1,-1, 1), new(-1,-1, 1)),
    (new( 0, 0, 1), new Color(220,  80, 200),
        new(-1,-1, 1), new( 1,-1, 1), new( 1, 1, 1), new(-1, 1, 1)),
    (new( 0, 0,-1), new Color( 80,  80, 220),
        new( 1,-1,-1), new(-1,-1,-1), new(-1, 1,-1), new( 1, 1,-1)),
};

var litVerts = new List<LitVertex3D>();
var litIdx   = new List<uint>();
foreach (var (n, c, a, b, cc, d) in faces)
{
    uint baseIndex = (uint)litVerts.Count;
    litVerts.Add(new LitVertex3D(a,  n, c));
    litVerts.Add(new LitVertex3D(b,  n, c));
    litVerts.Add(new LitVertex3D(cc, n, c));
    litVerts.Add(new LitVertex3D(d,  n, c));
    litIdx.Add(baseIndex + 0); litIdx.Add(baseIndex + 1); litIdx.Add(baseIndex + 2);
    litIdx.Add(baseIndex + 0); litIdx.Add(baseIndex + 2); litIdx.Add(baseIndex + 3);
}
var bigCube = Mesh.Create(litVerts.ToArray(), litIdx.ToArray());

// ---- Small marker cube: unlit white, just to show the light's position ---
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
        rd.DrawMesh(bigCube, Shaders.LitColor, new LightingArgs(bigModel));
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
        rd.DrawMesh(marker, Shaders.PositionColorWithTransform, markerModel);
    }

    w.Invalidate();
};

await window.WaitForCloseAsync();

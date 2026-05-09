#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/PointLights.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Demonstrates Blitter's point-light system. Lights live in a list on
// the renderer; lit shaders pick them up automatically through the
// IUniformArgs trait + a per-frame storage buffer the renderer uploads.
// Add or remove lights freely between frames -- the buffer grows on
// demand, no fixed cap.
//
// The scene: a tessellated white ground plane, a forest of small
// boxes, and three colored point lights orbiting overhead at
// different speeds and heights. Tiny unlit marker cubes show where
// each light sits so you can correlate the light position with the
// pool of color it casts on the ground.

using System.Numerics;
using Blitter;
using Blitter.Bits;

// ---- Geometry helpers --------------------------------------------------------

static (ColorVertex3D[] V, uint[] I) BuildSolidCube(float size, Color color)
{
    var h = size * 0.5f;
    var p = new Vector3[]
    {
        new(-h,-h,-h), new( h,-h,-h), new( h, h,-h), new(-h, h,-h),
        new(-h,-h, h), new( h,-h, h), new( h, h, h), new(-h, h, h),
    };
    var faces = new (int A, int B, int C, int D)[]
    {
        (1, 2, 6, 5), // +X
        (4, 7, 3, 0), // -X
        (3, 7, 6, 2), // +Y
        (4, 0, 1, 5), // -Y
        (5, 6, 7, 4), // +Z
        (0, 3, 2, 1), // -Z
    };
    var v = new List<ColorVertex3D>();
    var i = new List<uint>();
    foreach (var (a, b, c, d) in faces)
    {
        uint b0 = (uint)v.Count;
        v.Add(new ColorVertex3D(p[a], color));
        v.Add(new ColorVertex3D(p[b], color));
        v.Add(new ColorVertex3D(p[c], color));
        v.Add(new ColorVertex3D(p[d], color));
        i.Add(b0 + 0); i.Add(b0 + 1); i.Add(b0 + 2);
        i.Add(b0 + 0); i.Add(b0 + 2); i.Add(b0 + 3);
    }
    return (v.ToArray(), i.ToArray());
}

// ---- Build meshes ------------------------------------------------------------

var ground = Meshes.Plane(new Color(220, 220, 220), size: new Vector2(20f));
var box = Meshes.Cube(new Color(200, 200, 210), size: new Vector3(0.6f, 1.2f, 0.6f));

// Layout for the boxes: a loose grid, slightly jittered so it doesn't
// look like a chess board. Stationary across frames so the lights are
// the only moving thing.
var boxes = new List<Vector3>();
var rng = new Random(1234);
for (int z = -4; z <= 4; z++)
{
    for (int x = -4; x <= 4; x++)
    {
        if (x == 0 && z == 0) continue; // leave the center clear
        var jitter = new Vector3(
            (float)(rng.NextDouble() - 0.5) * 0.8f,
            0f,
            (float)(rng.NextDouble() - 0.5) * 0.8f);
        boxes.Add(new Vector3(x * 1.8f, 0.6f, z * 1.8f) + jitter);
    }
}

// Each light gets a tiny unlit marker cube colored to match. Building
// each mesh once (the BuildSolidCube call) and reusing it across
// frames -- otherwise the renderer's mesh cache would grow without
// bound and re-upload identical vertices every frame.
var redColor   = new Color(255,  90,  60);
var greenColor = new Color( 80, 255, 100);
var blueColor  = new Color( 80, 140, 255);
var (redV,   redI)   = BuildSolidCube(0.18f, redColor);
var (greenV, greenI) = BuildSolidCube(0.18f, greenColor);
var (blueV,  blueI)  = BuildSolidCube(0.18f, blueColor);
var redMarkerMesh    = Mesh.Create(redV,   redI);
var greenMarkerMesh  = Mesh.Create(greenV, greenI);
var blueMarkerMesh   = Mesh.Create(blueV,  blueI);

// ---- Window + camera ---------------------------------------------------------

var window = new Window3D
{
    Title = "Point lights: orbiting RGB over a populated ground",
    BackgroundColor = new Color(6, 6, 16),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 6.5f, 11f),
    Target = new Vector3(0f, 0.6f, 0f),
};

// Scaling factor on the rgb light colors. The unlit markers always
// look bright since they ignore lighting; the actual point lights
// also need enough intensity to overpower the directional + ambient
// terms in the patches they reach.
const float lightIntensity = 4f;
const float lightRange = 6f;

window.Rendering += (w, rd) =>
{
    rd.Camera = camera;

    // Just enough ambient to keep shadowed corners legible; a soft
    // overhead directional adds a baseline cool tone so the colored
    // pools really pop.
    rd.AmbientLight = new Color(20, 20, 30);
    rd.DirectionalLight = new DirectionalLight(
        Vector3.Normalize(new Vector3(0.2f, 1f, 0.4f)),
        new Color(40, 40, 70));

    // Three orbiting point lights at different radii, speeds, and
    // bobs. Mutate the renderer's PointLights list freely between
    // frames; the renderer snapshots and uploads it each frame.
    rd.PointLights.Clear();
    var t = (float)rd.ElapsedSinceStart.TotalSeconds;

    var redPos = new Vector3(MathF.Cos(t * 0.7f) * 4.5f, 1.6f + MathF.Sin(t * 1.1f) * 0.4f, MathF.Sin(t * 0.7f) * 4.5f);
    var greenPos = new Vector3(MathF.Cos(t * 0.9f + 2.0f) * 3.2f, 1.4f + MathF.Sin(t * 1.3f) * 0.5f, MathF.Sin(t * 0.9f + 2.0f) * 3.2f);
    var bluePos = new Vector3(MathF.Cos(-t * 0.5f + 4.0f) * 5.5f, 1.8f + MathF.Sin(t * 0.8f) * 0.3f, MathF.Sin(-t * 0.5f + 4.0f) * 5.5f);

    rd.PointLights.Add(new PointLight(redPos,   redColor,   lightRange, lightIntensity));
    rd.PointLights.Add(new PointLight(greenPos, greenColor, lightRange, lightIntensity));
    rd.PointLights.Add(new PointLight(bluePos,  blueColor,  lightRange, lightIntensity));

    using (rd.PushState())
    {
        rd.CullMode = CullMode.Back;

        // Lit ground.
        rd.DrawMesh(ground, ShaderSets.LitColor, new LitArgs(Matrix4x4.Identity));

        // Lit boxes.
        foreach (var p in boxes)
        {
            rd.DrawMesh(box, ShaderSets.LitColor, new LitArgs(Matrix4x4.CreateTranslation(p)));
        }

        // Unlit markers, one per light. Use PositionColorWithTransform
        // so they show their flat color regardless of lighting -- a
        // clear visual anchor for "the light is *here*."
        rd.DrawMesh(redMarkerMesh,   ShaderSets.PositionColorWithTransform, Matrix4x4.CreateTranslation(redPos));
        rd.DrawMesh(greenMarkerMesh, ShaderSets.PositionColorWithTransform, Matrix4x4.CreateTranslation(greenPos));
        rd.DrawMesh(blueMarkerMesh,  ShaderSets.PositionColorWithTransform, Matrix4x4.CreateTranslation(bluePos));
    }

    w.Invalidate();
};

await window.WaitForCloseAsync();

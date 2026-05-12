#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/InstancedLitCubes.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Demonstrates instanced drawing through the Materializer API: a grid
// of lit + textured cubes drawn in a single call. The mesh + material
// are supplied once; per-instance data (a world transform plus a tint
// color) is pulled from a span. Compare with TriangleSwarmInstanced
// (which calls Renderer3D directly) -- here the materializer picks
// the shader, so the call site doesn't reference any shader at all.

using System.Numerics;
using Blitter;
using Blitter.Bits;

const int Side = 8;                  // 8 x 8 = 64 cubes
const float Spacing = 1.6f;
const int Count = Side * Side;

// One mesh shared by every instance
var cubeMesh = Meshes.TexturedCube(size: new Vector3(0.7f));
var instances = new TransformAndColorInstance[Count];

var window = new Window3D
{
    Title = "Instanced lit cubes (one DrawInstanced call)",
    BackgroundColor = new Color(8, 8, 24),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 6f, 14f),
    Target = Vector3.Zero,
};

await window.RunAsync(rd =>
{
    rd.Camera = camera;
    rd.AmbientLight = new Color(40, 40, 60);

    var t = rd.ElapsedSecondsSinceStart;
    rd.DirectionalLight = new DirectionalLight(
        Vector3.Normalize(new Vector3(MathF.Cos(t * 0.4f), 0.8f, MathF.Sin(t * 0.4f))),
        Color.White);

    // Lay the cubes out on a grid centered on the origin; each cube
    // bobs up and down on a phase derived from its grid position, and
    // its tint cycles through the spectrum.
    float origin = -(Side - 1) * 0.5f * Spacing;
    int idx = 0;
    for (int z = 0; z < Side; z++)
    {
        for (int x = 0; x < Side; x++, idx++)
        {
            var cellPhase = (x + z) / (float)(Side * 2);
            var bob = 0.4f * MathF.Sin(t * 1.2f + cellPhase * MathF.Tau * 2f);
            var pos = new Vector3(
                origin + x * Spacing,
                bob,
                origin + z * Spacing);

            var spin = Matrix4x4.CreateRotationY(t * 0.6f + cellPhase * MathF.Tau);
            var transform = spin * Matrix4x4.CreateTranslation(pos);

            // Hue around the color wheel based on grid position.
            float hue = cellPhase + t * 0.05f;
            var tint = Color.FromHsv(hue, 0.6f, 1f);

            instances[idx] = new TransformAndColorInstance(transform, tint);
        }
    }

    using (rd.PushState())
    {
        rd.CullMode = CullMode.Back;
        // draws multiple instances of the same mesh in one call
        rd.DrawMesh(cubeMesh, instances);
    }
});

#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/PbrSpheres.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Standard "PBR test card": a 5x5 grid of spheres where rows vary
// metallic (0 -> 1, bottom -> top) and columns vary roughness
// (smooth -> rough, left -> right), all sharing one base color.
//
// What to look for:
//   * Bottom row (Metallic = 0, dielectric): leftmost sphere has a
//     sharp white highlight; moving right the highlight broadens and
//     blurs until the surface looks like matte plastic.
//   * Top row (Metallic = 1, metal): the entire sphere tints toward
//     the base color and the highlight takes the base-color tint.
//   * Mid rows show the transition; F0 (the reflection at grazing
//     angles) shifts smoothly from a tiny dielectric Fresnel rim to
//     a fully colored metallic reflection.
//
// The camera orbits slowly so the specular highlights crawl across
// the spheres -- that motion is the most legible PBR cue; a static
// shot looks like 25 colored plastic balls.

using System.Numerics;
using Blitter;
using Blitter.Bits;

const int Rows = 5;       // metallic axis
const int Cols = 5;       // roughness axis
const float Spacing = 1.2f;
const float Radius = 0.5f;

// One shared sphere mesh -- material varies per draw, geometry doesn't.
// LitTextureVertex3D is what PbrShaders.LitPbr binds against; vertex
// color is left at white so BaseColor controls tint exclusively.
var sphere = Meshes.TexturedSphere(
    radius: Radius, latitudeSegments: 32, longitudeSegments: 48);

// A single warm base color shared by every sphere; reads as a
// reddish-bronze when the metallic factor is high.
var baseColor = new Color(220, 170, 130);
//var baseColor = Color.Yellow;

// Pre-build all 25 materials so the per-frame loop is just draws.
var materials = new PbrMaterial[Rows * Cols];
for (int row = 0; row < Rows; row++)
{
    float metallic = row / (float)(Rows - 1);
    for (int col = 0; col < Cols; col++)
    {
        // Clamp roughness off zero -- a perfect mirror produces a
        // singular highlight (one pixel wide) that aliases badly
        // without IBL; 0.05 still reads as "very polished".
        float roughness = MathF.Max(0.05f, col / (float)(Cols - 1));
        materials[row * Cols + col] = new PbrMaterial
        {
            BaseColor = baseColor,
            Metallic = metallic,
            Roughness = roughness,
        };
    }
}

var window = new Window3D(800, 600)
{
    Title = "PBR test card: metallic (rows) x roughness (cols)",
    BackgroundColor = new Color(12, 12, 18),
    //FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera();

window.Renderer.Camera = camera;

// add environment lighting environment for the spheres to reflect
window.Renderer.EnvironmentLight = EnvironmentLights.SkySunless;

// set the ambient light to a bright color so we can see the environment reflections
window.Renderer.AmbientLight = new Color(180, 200, 230);

// Add directional light to create a clear primary highlight on each sphere.
window.Renderer.DirectionalLight = new DirectionalLight(
    Vector3.Normalize(new Vector3(-0.4f, 0.7f, 0.6f)),
    Color.White);

await window.RunAsync(rd =>
{ 
    // No point lights: with multiple lights on these spheres the
    // hard NdotL terminator from each one creates banded colored
    // patches that read as "patterned" rather than "metallic." The
    // shader's hemisphere ambient fills the same role (a soft
    // environment fill) without any per-light boundaries.
    var t = rd.ElapsedSecondsSinceStart;
    float gridWidth = (Cols - 1) * Spacing;
    float gridHeight = (Rows - 1) * Spacing;
    var center = new Vector3(gridWidth * 0.5f, gridHeight * 0.5f, 0f);

    // Camera orbits the grid center at a fixed height. Speed is slow
    // enough that you can read the highlight shape on each sphere
    // before it changes.
    // Orbit radius tuned so a 5x5 grid of unit-diameter spheres fills
    // most of a 16:9 viewport.
    camera.Position = center + new Vector3(
        MathF.Sin(t * 0.25f) * 7.5f,
        gridHeight * 0.15f,
        MathF.Cos(t * 0.25f) * 7.5f);
    camera.Target = center;

    using (rd.PushState())
    {
        rd.CullMode = CullMode.Back;

        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                var position = new Vector3(
                    col * Spacing,
                    row * Spacing,
                    0f);
                var model = Matrix4x4.CreateTranslation(position);
                rd.DrawMesh(sphere, materials[row * Cols + col], model);
                //rd.DrawMesh(sphere, materials[0], model);
            }
        }
    }
});

#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/SkyCubemap.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Displays Blitter.Bits's default procedural sky cubemap as a
// skybox. The sky is baked once (CPU per-pixel) and cached for the
// rest of the process, so accessing `Cubemaps.Sky` again is free.

using System.Numerics;
using Blitter;
using Blitter.Bits;

var skyboxVertices = new Vertex3D[]
{
    new(-1, -1, -1), new( 1, -1, -1), new( 1,  1, -1), new(-1,  1, -1),
    new(-1, -1,  1), new( 1, -1,  1), new( 1,  1,  1), new(-1,  1,  1),
};
var skyboxIndices = new uint[]
{
    4, 5, 6,  4, 6, 7,   1, 0, 3,  1, 3, 2,
    0, 4, 7,  0, 7, 3,   5, 1, 2,  5, 2, 6,
    7, 6, 2,  7, 2, 3,   0, 1, 5,  0, 5, 4,
};
var skyboxMesh = Mesh.Create(skyboxVertices, skyboxIndices);

var window = new Window3D
{
    Title = "Cubemaps.Sky: default procedural sky",
    BackgroundColor = Color.Black,
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera();
await window.RunAsync(rd =>
{
    var t = rd.ElapsedSecondsSinceStart;
    // Camera at origin, looking around. Yaw sweeps continuously;
    // pitch sweeps slowly so we see ground -> horizon -> zenith.
    float yaw = t * 0.3f;
    float pitch = MathF.Sin(t * 0.2f) * (MathF.PI / 2.05f);
    camera.Position = Vector3.Zero;
    camera.Target = new Vector3(
        MathF.Cos(pitch) * MathF.Sin(yaw),
        MathF.Sin(pitch),
        MathF.Cos(pitch) * MathF.Cos(yaw));

    using (rd.PushState())
    {
        rd.CullMode = CullMode.None;
        rd.DrawMeshRaw(skyboxMesh, Cubemaps.Sky, Shaders.Skybox,
            camera.GetSkyboxViewProjection(rd.AspectRatio));
    }
});

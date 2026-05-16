#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/DiffuseCubemap.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// Shows side-by-side the basic procedural sky box and its diffused counterpart.

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

var sky = Cubemaps.Sky;
var diffuse = Cubemaps.SkyDiffuse;

var window = new Window3D
{
    Title = "Cubemaps.SkyDiffuse: sky (left) vs diffuse (right)",
    BackgroundColor = Color.Black,
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera();
await window.RunAsync(rd =>
{
    var t = rd.ElapsedSecondsSinceStart;
    float yaw = t * 0.3f;
    float pitch = MathF.Sin(t * 0.2f) * (MathF.PI / 2.05f);
    camera.Position = Vector3.Zero;
    camera.Target = new Vector3(
        MathF.Cos(pitch) * MathF.Sin(yaw),
        MathF.Sin(pitch),
        MathF.Cos(pitch) * MathF.Cos(yaw));

    var (w, h) = window.Size;
    float paneW = w / 2f;
    float paneAspect = paneW / h;
    var skyboxVp = camera.GetSkyboxViewProjection(paneAspect);

    using (rd.PushState())
    {
        rd.Viewport = new Rect(0, 0, paneW, h);
        rd.CullMode = CullMode.None;
        rd.DrawMeshRaw(skyboxMesh, sky, Shaders.Skybox, skyboxVp);
    }

    using (rd.PushState())
    {
        rd.Viewport = new Rect(paneW, 0, paneW, h);
        rd.CullMode = CullMode.None;
        rd.DrawMeshRaw(skyboxMesh, diffuse, Shaders.Skybox, skyboxVp);
    }
});

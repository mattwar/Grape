#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/WireframeCube.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// Two cubes side-by-side, sharing the same mesh, one solide, one wireframe.

using System.Numerics;
using Blitter;
using Blitter.Bits;

// 8 unique cube corners + 36 triangle indices (12 triangles, CCW from outside). 
// Identical to the IndexedCube sample.
var vertices = new ColorVertex3D[]
{
    new(new Vertex3D(-1f, -1f, -1f), new Color(40,  40, 200)),
    new(new Vertex3D( 1f, -1f, -1f), new Color(200, 40,  40)),
    new(new Vertex3D( 1f,  1f, -1f), new Color(200, 200, 40)),
    new(new Vertex3D(-1f,  1f, -1f), new Color(40,  200, 40)),
    new(new Vertex3D(-1f, -1f,  1f), new Color(40,  200, 200)),
    new(new Vertex3D( 1f, -1f,  1f), new Color(200, 40,  200)),
    new(new Vertex3D( 1f,  1f,  1f), new Color(255, 255, 255)),
    new(new Vertex3D(-1f,  1f,  1f), new Color(80,  80,  80)),
};

var indices = new uint[]
{
    4, 5, 6,   4, 6, 7,    // +Z front
    1, 0, 3,   1, 3, 2,    // -Z back
    0, 4, 7,   0, 7, 3,    // -X left
    5, 1, 2,   5, 2, 6,    // +X right
    7, 6, 2,   7, 2, 3,    // +Y top
    0, 1, 5,   0, 5, 4,    // -Y bottom
};

var cube = Mesh.Create(vertices, indices);

var window = new Window3D
{
    Title = "Wireframe: same mesh, drawn solid (left) and wireframe (right)",
    BackgroundColor = new Color(8, 8, 24),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 1.0f, 6.5f),
};

await window.RunAsync(rd =>
{
    var t = rd.ElapsedSecondsSinceStart;
    var viewProjection = camera.GetViewProjection(rd);

    var spin = Matrix4x4.CreateRotationY(t * 0.7f)
        .RotateX(t * 0.4f);

    var modelLeft  = spin * Matrix4x4.CreateTranslation(-1.8f, 0f, 0f);
    var modelRight = spin * Matrix4x4.CreateTranslation( 1.8f, 0f, 0f);

    // solid cube
    using (rd.PushState())
    {
        rd.CullMode = CullMode.Back;
        rd.DrawMesh(cube, Shaders.PositionColorWithTransform, modelLeft * viewProjection);
    }

    // wireframe cube
    using (rd.PushState())
    {
        rd.Wireframe = true;
        rd.DrawMesh(cube, Shaders.PositionColorWithTransform, modelRight * viewProjection);
    }

    // Labels.
    using (rd.PushState())
    {
        rd.DepthMode = DepthMode.Overlay;

        DrawLabel(rd, "Solid",     offsetX: -1.8f, viewProjection);
        DrawLabel(rd, "Wireframe", offsetX:  1.8f, viewProjection);
    }
});

static void DrawLabel(Renderer3D renderer, string text, float offsetX, Matrix4x4 viewProjection)
{
    const float scale = 0.08f;
    var transform = Matrix4x4.CreateTranslation(-text.Length / 2f, 0f, 0f)
        .Scale(scale)
        .Translate(offsetX, -1.7f, 0f);
    renderer.DrawDebugText(text, transform * viewProjection);
}

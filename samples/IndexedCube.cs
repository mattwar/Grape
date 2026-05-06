#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/IndexedCube.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// A spinning cube built from an indexed mesh: 8 unique vertices reused
// across 12 triangles via 36 indices. Without indices the same geometry
// would need 36 vertices -- one full copy of position + color for every
// triangle corner, even though every vertex is shared by three faces.
//
// CullMode.Back is on. Because the cube is a closed solid, every back
// face is hidden behind a front face anyway, so culling them is pure
// savings: the GPU drops half the triangles before rasterisation.

using System.Numerics;
using Grape;

// 8 unique corners of a unit cube centred on the origin. Each gets its
// own color so face interpolation makes the cube's structure obvious.
//
//        v3---------v2
//       /|         /|
//      v7---------v6|
//      | |        | |
//      | v0-------|v1
//      |/         |/
//      v4---------v5
//
// Axes: +X right, +Y up, +Z out of the screen toward the camera.
var vertices = new ColorVertex3D[]
{
    new(new Vertex3D(-1f, -1f, -1f), new Color(40,  40, 200)),  // 0: back-bottom-left
    new(new Vertex3D( 1f, -1f, -1f), new Color(200, 40,  40)),  // 1: back-bottom-right
    new(new Vertex3D( 1f,  1f, -1f), new Color(200, 200, 40)),  // 2: back-top-right
    new(new Vertex3D(-1f,  1f, -1f), new Color(40,  200, 40)),  // 3: back-top-left
    new(new Vertex3D(-1f, -1f,  1f), new Color(40,  200, 200)), // 4: front-bottom-left
    new(new Vertex3D( 1f, -1f,  1f), new Color(200, 40,  200)), // 5: front-bottom-right
    new(new Vertex3D( 1f,  1f,  1f), new Color(255, 255, 255)), // 6: front-top-right
    new(new Vertex3D(-1f,  1f,  1f), new Color(80,  80,  80)),  // 7: front-top-left
};

// 12 triangles, 3 indices each. Each face's triangles are wound
// counter-clockwise when viewed from outside the cube so the GPU
// classifies them as front-facing under the default right-hand rule.
var indices = new uint[]
{
    // Front face (+Z): looking from +Z, CCW
    4, 5, 6,   4, 6, 7,
    // Back face (-Z): looking from -Z, CCW (so reversed from +Z view)
    1, 0, 3,   1, 3, 2,
    // Left face (-X): looking from -X, CCW
    0, 4, 7,   0, 7, 3,
    // Right face (+X): looking from +X, CCW
    5, 1, 2,   5, 2, 6,
    // Top face (+Y): looking from +Y, CCW
    7, 6, 2,   7, 2, 3,
    // Bottom face (-Y): looking from -Y, CCW
    0, 1, 5,   0, 5, 4,
};

var cube = Mesh.Create<ColorVertex3D>(vertices, indices);

var window = new Window3D
{
    Title = "Indexed cube: 8 vertices, 36 indices",
    BackgroundColor = new Color(8, 8, 24),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 1.5f, 5f),
};

window.Rendering += (w, e) =>
{
    var t = (float)e.ElapsedSinceWindowCreated.TotalSeconds;
    var (width, height) = w.Size;
    var viewProjection = camera.GetViewProjection((float)width / height);

    var model =
        Matrix4x4.CreateRotationY(t * 0.7f) *
        Matrix4x4.CreateRotationX(t * 0.4f);

    // Closed solid -> back faces are always hidden by front faces, so
    // culling them costs nothing visually and saves the rasteriser
    // half the triangles.
    using (e.Renderer.PushState())
    {
        e.Renderer.CullMode = CullMode.Back;
        e.Renderer.RenderMesh(cube, Shaders.PositionColorWithTransform, model * viewProjection);
    }

    // Caption sits in front of everything regardless of depth.
    using (e.Renderer.PushState())
    {
        e.Renderer.DepthMode = DepthMode.Overlay;
        e.Renderer.CullMode = CullMode.None;

        DrawLabel(e.Renderer, "8 vertices, 36 indices (vs 36 vertices unindexed)",
            yOffset: -1.6f, viewProjection);
    }

    w.Invalidate();
};

static void DrawLabel(Renderer3D renderer, string text, float yOffset, Matrix4x4 viewProjection)
{
    const float scale = 0.08f;
    var transform =
        Matrix4x4.CreateTranslation(-text.Length / 2f, 0f, 0f) *
        Matrix4x4.CreateScale(scale) *
        Matrix4x4.CreateTranslation(0f, yOffset, 0f) *
        viewProjection;
    renderer.RenderDebugText(text, transform);
}

await window.WaitForCloseAsync();

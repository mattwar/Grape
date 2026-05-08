#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/WireframeCube.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// Two cubes side-by-side, sharing the same mesh:
//
//   * Left:  solid (default)
//   * Right: wireframe -- the renderer derives the cube's unique edges
//            from its triangle indices, caches them, and binds a
//            LineList pipeline. Shared edges between adjacent triangles
//            are deduped so each visible edge is drawn exactly once.
//
// Wireframe is a per-draw renderer state, scoped via PushState() like
// CullMode and DepthMode. Toggling it on a triangle-based mesh costs
// one extra index-buffer build per change; non-triangle meshes ignore
// the flag.

using System.Numerics;
using Grape;

// 8 unique cube corners + 36 triangle indices (12 triangles, CCW from
// outside). Identical to the IndexedCube sample.
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

window.Rendering += (w, rd) =>
{
    var t = (float)rd.ElapsedSinceStart.TotalSeconds;
    var (width, height) = w.Size;
    var viewProjection = camera.GetViewProjection((float)width / height);

    var spin =
        Matrix4x4.CreateRotationY(t * 0.7f) *
        Matrix4x4.CreateRotationX(t * 0.4f);

    var modelLeft  = spin * Matrix4x4.CreateTranslation(-1.8f, 0f, 0f);
    var modelRight = spin * Matrix4x4.CreateTranslation( 1.8f, 0f, 0f);

    // Left cube: solid. CullMode.Back is safe because the cube is a
    // closed solid -- back faces are always hidden inside.
    using (rd.PushState())
    {
        rd.CullMode = CullMode.Back;
        rd.DrawMesh(cube, ShaderSets.PositionColorWithTransform, modelLeft * viewProjection);
    }

    // Right cube: wireframe. The renderer builds a deduped edge index
    // buffer (12 unique edges -> 24 line indices) the first time this
    // draws and reuses it on every subsequent frame.
    //
    // CullMode is left at None for wireframe -- there are no faces to
    // cull, just lines, and lines have no facing.
    using (rd.PushState())
    {
        rd.Wireframe = true;
        rd.DrawMesh(cube, ShaderSets.PositionColorWithTransform, modelRight * viewProjection);
    }

    // Labels.
    using (rd.PushState())
    {
        rd.DepthMode = DepthMode.Overlay;

        DrawLabel(rd, "Solid",     offsetX: -1.8f, viewProjection);
        DrawLabel(rd, "Wireframe", offsetX:  1.8f, viewProjection);
    }

    w.Invalidate();
};

static void DrawLabel(Renderer3D renderer, string text, float offsetX, Matrix4x4 viewProjection)
{
    const float scale = 0.08f;
    var transform =
        Matrix4x4.CreateTranslation(-text.Length / 2f, 0f, 0f) *
        Matrix4x4.CreateScale(scale) *
        Matrix4x4.CreateTranslation(offsetX, -1.7f, 0f) *
        viewProjection;
    renderer.DrawDebugText(text, transform);
}

await window.WaitForCloseAsync();

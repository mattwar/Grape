#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/CullingComparison.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// Two identical single-faced quads spin side-by-side. The left one uses
// CullMode.None (both sides drawn) and stays visible throughout its
// rotation. The right one uses CullMode.Back -- when its front face
// rotates away from the camera, the GPU skips the triangles entirely,
// so the quad blinks out for half of every revolution.
//
// This is the difference back-face culling makes. For closed solid
// meshes, the culled half is always *inside* the solid and never
// visible anyway -- so it's pure savings. For single-faced or open
// geometry like this quad, you can see exactly what gets thrown away.

using System.Numerics;
using Grape;

// A unit quad facing +Z, wound counter-clockwise when viewed from +Z so
// "front" lines up with the right-hand rule.
//
//   v0 (-1, 1)   v3 (1, 1)
//        +----------+
//        |        / |
//        |      /   |
//        |    /     |
//        |  /       |
//        +----------+
//   v1 (-1,-1)   v2 (1,-1)
//
static Mesh<ColorVertex3D> MakeQuad(Color color)
{
    var v0 = new ColorVertex3D(new Vertex3D(-1f,  1f, 0f), color);
    var v1 = new ColorVertex3D(new Vertex3D(-1f, -1f, 0f), color);
    var v2 = new ColorVertex3D(new Vertex3D( 1f, -1f, 0f), color);
    var v3 = new ColorVertex3D(new Vertex3D( 1f,  1f, 0f), color);

    return Mesh.Create([
        v0, v1, v2,   // first triangle (CCW from +Z)
        v0, v2, v3,   // second triangle (CCW from +Z)
    ]);
}

var quadLeft  = MakeQuad(new Color(80, 200, 255));   // cyan
var quadRight = MakeQuad(new Color(255, 160, 80));   // amber

// A backdrop strip placed behind both quads. When the right quad culls
// its back face, the missing pixels reveal this backdrop -- proving
// they really are absent rather than just blending with the window
// background. A four-corner gradient makes the reveal even more
// obvious.
var backdrop = Mesh.Create(new ColorVertex3D[]
{
    new(new Vertex3D(-3.0f,  1.2f, 0f), new Color( 80,  40, 120)),  // top-left
    new(new Vertex3D(-3.0f, -1.2f, 0f), new Color(120,  80,  40)),  // bottom-left
    new(new Vertex3D( 3.0f, -1.2f, 0f), new Color( 40, 120,  80)),  // bottom-right
    new(new Vertex3D(-3.0f,  1.2f, 0f), new Color( 80,  40, 120)),  // top-left
    new(new Vertex3D( 3.0f, -1.2f, 0f), new Color( 40, 120,  80)),  // bottom-right
    new(new Vertex3D( 3.0f,  1.2f, 0f), new Color(120,  40, 120)),  // top-right
});

var window = new Window3D
{
    Title = "Backface culling: None (left) vs Back (right)",
    BackgroundColor = new Color(0, 0, 32),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 0f, 5f),
};

window.Rendering += (w, rd) =>
{
    var t = (float)rd.ElapsedSinceStart.TotalSeconds;
    var (width, height) = w.Size;
    var viewProjection = camera.GetViewProjection((float)width / height);

    // Both quads spin around Y at the same rate, just at different
    // positions. Spinning around Y means the front face faces the
    // camera for half the rotation and away from it for the other half.
    var spin = Matrix4x4.CreateRotationY(t * 1.0f);

    var modelLeft  = Matrix4x4.CreateScale(0.6f) * spin *
                     Matrix4x4.CreateTranslation(-1.5f, 0f, 0f);
    var modelRight = Matrix4x4.CreateScale(0.6f) * spin *
                     Matrix4x4.CreateTranslation( 1.5f, 0f, 0f);

    // Backdrop sits behind both quads (negative Z relative to the camera
    // looking down -Z) so the depth buffer correctly orders it under
    // everything else. When the right quad's back face is culled, the
    // missing pixels reveal this gradient.
    var modelBackdrop = Matrix4x4.CreateTranslation(0f, 0f, -1f);
    rd.DrawMesh(backdrop, ShaderSets.PositionColorWithTransform, modelBackdrop * viewProjection);

    // Left: CullMode.None. Both faces drawn, quad always visible.
    using (rd.PushState())
    {
        rd.CullMode = CullMode.None;
        rd.DrawMesh(quadLeft, ShaderSets.PositionColorWithTransform, modelLeft * viewProjection);
    }

    // Right: CullMode.Back. Front face only; the quad disappears when
    // its CCW-wound side rotates away from the camera.
    using (rd.PushState())
    {
        rd.CullMode = CullMode.Back;
        rd.DrawMesh(quadRight, ShaderSets.PositionColorWithTransform, modelRight * viewProjection);
    }

    // Labels. Debug text uses textured quads, so we draw them as
    // overlays so they aren't culled or occluded by the spinning quads.
    using (rd.PushState())
    {
        rd.DepthMode = DepthMode.Overlay;
        rd.CullMode = CullMode.None;

        DrawLabel(rd, "CullMode.None", offsetX: -1.5f, viewProjection);
        DrawLabel(rd, "CullMode.Back", offsetX:  1.5f, viewProjection);
    }

    w.Invalidate();
};

static void DrawLabel(Renderer3D renderer, string text, float offsetX, Matrix4x4 viewProjection)
{
    const float scale = 0.08f;
    var transform =
        Matrix4x4.CreateTranslation(-text.Length / 2f, 0f, 0f) *
        Matrix4x4.CreateScale(scale) *
        Matrix4x4.CreateTranslation(offsetX, -1.2f, 0f) *
        viewProjection;
    renderer.DrawDebugText(text, transform);
}

await window.WaitForCloseAsync();

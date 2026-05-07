#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/SplitScreen3D.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// The samples/NuGet.config in this folder pulls Grape.Graphics from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.

// Two cameras rendering the same scene side-by-side, demonstrating
// Renderer3D.Viewport. Each pane uses PushState() so its viewport
// scope ends cleanly and the next pane's setting is independent.

using System.Numerics;
using Grape;

static Mesh<ColorVertex3D> MakeTetrahedron(Color c0, Color c1, Color c2, Color c3)
{
    var v0 = new Vertex3D( 1f,  1f,  1f);
    var v1 = new Vertex3D( 1f, -1f, -1f);
    var v2 = new Vertex3D(-1f,  1f, -1f);
    var v3 = new Vertex3D(-1f, -1f,  1f);

    return Mesh.Create([
        new ColorVertex3D(v1, c1), new ColorVertex3D(v2, c2), new ColorVertex3D(v3, c3),
        new ColorVertex3D(v0, c0), new ColorVertex3D(v3, c3), new ColorVertex3D(v2, c2),
        new ColorVertex3D(v0, c0), new ColorVertex3D(v1, c1), new ColorVertex3D(v3, c3),
        new ColorVertex3D(v0, c0), new ColorVertex3D(v2, c2), new ColorVertex3D(v1, c1),
    ]);
}

var tetraA = MakeTetrahedron(
    new Color(255, 64, 64),
    new Color(255, 200, 0),
    new Color(255, 128, 0),
    new Color(200, 32, 96));

var tetraB = MakeTetrahedron(
    new Color(64, 200, 255),
    new Color(0, 255, 200),
    new Color(64, 96, 255),
    new Color(160, 64, 255));

var window = new Window3D
{
    Title = "Split Screen (viewport demo)",
    BackgroundColor = new Color(0, 0, 32),
    FullScreen = true,
    CloseKey = Key.Escape,
};

const float OrbitRadius = 1.2f;
const float OrbitSpeed = 0.6f;
const float SpinSpeed = 1.5f;
const float TetraScale = 0.7f;

// Two cameras viewing the same scene from different angles.
var cameraLeft = new PerspectiveCamera
{
    Position = new Vector3(0f, 0.6f, 5f),
};
var cameraRight = new PerspectiveCamera
{
    Position = new Vector3(4f, 2f, 2f),
    Target = Vector3.Zero,
};

void DrawScene(Renderer3D r, Matrix4x4 viewProjection, float t)
{
    var orbitA = new Vector3(MathF.Cos(t * OrbitSpeed), 0f, MathF.Sin(t * OrbitSpeed)) * OrbitRadius;
    var orbitB = -orbitA;

    var spinA = Matrix4x4.CreateRotationY(t * SpinSpeed) *
                Matrix4x4.CreateRotationX(t * SpinSpeed * 0.7f);
    var spinB = Matrix4x4.CreateRotationY(-t * SpinSpeed) *
                Matrix4x4.CreateRotationZ(t * SpinSpeed * 0.5f);

    var modelA = Matrix4x4.CreateScale(TetraScale) * spinA * Matrix4x4.CreateTranslation(orbitA);
    var modelB = Matrix4x4.CreateScale(TetraScale) * spinB * Matrix4x4.CreateTranslation(orbitB);

    r.DrawMesh(tetraA, Shaders.PositionColorWithTransform, modelA * viewProjection);
    r.DrawMesh(tetraB, Shaders.PositionColorWithTransform, modelB * viewProjection);
}

window.Rendering += (w, rd) =>
{
    var t = (float)rd.ElapsedSinceStart.TotalSeconds;
    var (width, height) = w.Size;

    // Each pane is half the window's width. The aspect ratio passed to
    // the camera also halves so the scene isn't horizontally squished.
    var paneWidth = width / 2f;
    var paneAspect = paneWidth / height;

    // PushState() snapshots Viewport (and the rest of the renderer
    // state) so when this scope ends Viewport reverts to its previous
    // value. The next pane sets its own viewport independently.
    using (rd.PushState())
    {
        rd.Viewport = new Rect(0, 0, paneWidth, height);
        DrawScene(rd, cameraLeft.GetViewProjection(paneAspect), t);
    }

    using (rd.PushState())
    {
        rd.Viewport = new Rect(paneWidth, 0, paneWidth, height);
        DrawScene(rd, cameraRight.GetViewProjection(paneAspect), t);
    }

    w.Invalidate();
};

await window.WaitForCloseAsync();

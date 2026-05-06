#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/OverlayTetrahedron.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// Builds on OrbitingTetrahedra to demonstrate DepthMode.Overlay: a third
// "indicator" tetrahedron orbits the same axis as the other two, but is
// drawn with DepthMode.Overlay inside a PushState() scope so it always
// appears on top -- even when its 3D position would put it behind the
// solid tetras. After the scope ends, drawing returns to the default
// depth mode automatically.

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

// Bright magenta indicator -- always-on-top "selection marker" style.
var indicator = MakeTetrahedron(
    new Color(255, 0, 255),
    new Color(255, 128, 255),
    new Color(200, 0, 200),
    new Color(255, 64, 200));

var window = new Window3D
{
    Title = "Overlay Tetrahedron (DepthMode.Overlay demo)",
    BackgroundColor = new Color(0, 0, 32),
    FullScreen = true,
    CloseKey = Key.Escape,
};

const float OrbitRadius = 1.2f;
const float OrbitSpeed = 0.6f;
const float SpinSpeed = 1.5f;
const float TetraScale = 0.7f;
const float IndicatorScale = 0.35f;

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 0.6f, 5f),
};

window.Rendering += (w, e) =>
{
    var t = (float)e.ElapsedSinceStart.TotalSeconds;
    var (width, height) = w.Size;
    var viewProjection = camera.GetViewProjection((float)width / height);

    // Two solid orbiting tetras -- same as the OrbitingTetrahedra sample.
    var orbitA = new Vector3(MathF.Cos(t * OrbitSpeed), 0f, MathF.Sin(t * OrbitSpeed)) * OrbitRadius;
    var orbitB = -orbitA;

    var spinA = Matrix4x4.CreateRotationY(t * SpinSpeed) *
                Matrix4x4.CreateRotationX(t * SpinSpeed * 0.7f);
    var spinB = Matrix4x4.CreateRotationY(-t * SpinSpeed) *
                Matrix4x4.CreateRotationZ(t * SpinSpeed * 0.5f);

    var modelA = Matrix4x4.CreateScale(TetraScale) * spinA * Matrix4x4.CreateTranslation(orbitA);
    var modelB = Matrix4x4.CreateScale(TetraScale) * spinB * Matrix4x4.CreateTranslation(orbitB);

    e.DrawMesh(tetraA, Shaders.PositionColorWithTransform, modelA * viewProjection);
    e.DrawMesh(tetraB, Shaders.PositionColorWithTransform, modelB * viewProjection);

    // The indicator orbits faster, on a tilted ring that passes through
    // the centre of the scene -- which means at some moments its true 3D
    // position is well behind both solid tetras. Without DepthMode.Overlay
    // it would be occluded; with it, it always draws on top.
    var indicatorAngle = t * 1.8f;
    var indicatorOrbit = new Vector3(
        MathF.Cos(indicatorAngle) * 1.5f,
        MathF.Sin(indicatorAngle * 0.7f) * 0.6f,
        MathF.Sin(indicatorAngle) * 1.5f);
    var indicatorSpin = Matrix4x4.CreateRotationY(t * 3f) *
                        Matrix4x4.CreateRotationX(t * 2f);
    var indicatorModel = Matrix4x4.CreateScale(IndicatorScale) *
                         indicatorSpin *
                         Matrix4x4.CreateTranslation(indicatorOrbit);

    using (e.PushState())
    {
        e.DepthMode = DepthMode.Overlay;
        e.DrawMesh(indicator, Shaders.PositionColorWithTransform, indicatorModel * viewProjection);
    } // DepthMode automatically restored to Default here.

    w.Invalidate();
};

await window.WaitForCloseAsync();

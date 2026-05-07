#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/OrbitingTetrahedra.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// The samples/NuGet.config in this folder pulls Grape.Graphics from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.

// Two regular tetrahedra orbit a common axis in the XZ plane while
// independently spinning. As one tetra moves behind the other (greater
// distance from the camera) the depth buffer ensures it is correctly
// occluded -- and they intersect when their orbits put them at the same
// depth, with the depth buffer resolving the intersection per-pixel.

using System.Numerics;
using Grape;

// Regular tetrahedron with vertices on the unit cube's diagonals.
// Each vertex carries a color so the faces show as colored gradients
// once they are interpolated across the triangle.
static Mesh<ColorVertex3D> MakeTetrahedron(Color c0, Color c1, Color c2, Color c3)
{
    var v0 = new Vertex3D( 1f,  1f,  1f);
    var v1 = new Vertex3D( 1f, -1f, -1f);
    var v2 = new Vertex3D(-1f,  1f, -1f);
    var v3 = new Vertex3D(-1f, -1f,  1f);

    return Mesh.Create([
        // face opposite v0
        new ColorVertex3D(v1, c1), new ColorVertex3D(v2, c2), new ColorVertex3D(v3, c3),
        // face opposite v1
        new ColorVertex3D(v0, c0), new ColorVertex3D(v3, c3), new ColorVertex3D(v2, c2),
        // face opposite v2
        new ColorVertex3D(v0, c0), new ColorVertex3D(v1, c1), new ColorVertex3D(v3, c3),
        // face opposite v3
        new ColorVertex3D(v0, c0), new ColorVertex3D(v2, c2), new ColorVertex3D(v1, c1),
    ]);
}

// Warm-toned tetra and cool-toned tetra so it's obvious which is which
// when they overlap.
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
    Title = "Orbiting Tetrahedra (depth-buffer demo)",
    BackgroundColor = new Color(0, 0, 32),
    FullScreen = true,
    CloseKey = Key.Escape,
};

const float OrbitRadius = 1.2f;
const float OrbitSpeed = 0.6f;     // radians/sec
const float SpinSpeed = 1.5f;      // self-rotation, radians/sec
const float TetraScale = 0.7f;

// Camera at +Z looking toward the origin. With System.Numerics's
// right-handed perspective, larger Z = closer to the camera.
var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 0.6f, 5f),
};

window.Rendering += (w, r) =>
{
    var t = (float)r.ElapsedSinceStart.TotalSeconds;
    var (width, height) = w.Size;
    var viewProjection = camera.GetViewProjection((float)width / height);

    // Tetra A and B sit on opposite sides of the orbit; as `t` advances
    // they swap places in Z, and pass through each other near the centre.
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

    w.Invalidate();
};

await window.WaitForCloseAsync();

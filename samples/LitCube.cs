#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/LitCube.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// Demonstrates Grape's directional + ambient lighting. The cube uses
// per-face normals (so each face is uniformly shaded by its angle to
// the light), with a flat baked color per face. As the cube rotates,
// faces brighten and darken under the directional light, with the
// ambient term keeping the dark side from going pitch black.
//
// Lighting is composed automatically: the renderer's Camera,
// AmbientLight, and DirectionalLight properties feed the LitColor
// shader through the IUniformArgs trait mechanism, so the per-draw
// arguments only carry the user-supplied model matrix.

using System.Numerics;
using Grape;

// Six faces, four vertices each, with per-face normals and a per-face
// color. Per-face (not per-vertex) normals give crisp flat shading;
// using per-vertex averaged normals would smooth-shade the cube into
// a sphere-ish blob.
var faces = new (Vector3 Normal, Color Color, Vector3 A, Vector3 B, Vector3 C, Vector3 D)[]
{
    // +X (right)   red
    (new( 1, 0, 0), new Color(220,  60,  60),
        new( 1,-1,-1), new( 1, 1,-1), new( 1, 1, 1), new( 1,-1, 1)),
    // -X (left)    cyan
    (new(-1, 0, 0), new Color( 60, 200, 220),
        new(-1,-1, 1), new(-1, 1, 1), new(-1, 1,-1), new(-1,-1,-1)),
    // +Y (top)     green
    (new( 0, 1, 0), new Color( 80, 200,  80),
        new(-1, 1, 1), new( 1, 1, 1), new( 1, 1,-1), new(-1, 1,-1)),
    // -Y (bottom)  yellow
    (new( 0,-1, 0), new Color(220, 200,  60),
        new(-1,-1,-1), new( 1,-1,-1), new( 1,-1, 1), new(-1,-1, 1)),
    // +Z (front)   magenta
    (new( 0, 0, 1), new Color(220,  80, 200),
        new(-1,-1, 1), new( 1,-1, 1), new( 1, 1, 1), new(-1, 1, 1)),
    // -Z (back)    blue
    (new( 0, 0,-1), new Color( 80,  80, 220),
        new( 1,-1,-1), new(-1,-1,-1), new(-1, 1,-1), new( 1, 1,-1)),
};

var verts = new List<LitVertex3D>();
var idx   = new List<uint>();
foreach (var (n, c, a, b, cc, d) in faces)
{
    uint baseIndex = (uint)verts.Count;
    verts.Add(new LitVertex3D(a,  n, c));
    verts.Add(new LitVertex3D(b,  n, c));
    verts.Add(new LitVertex3D(cc, n, c));
    verts.Add(new LitVertex3D(d,  n, c));
    // Two CCW triangles per quad (when viewed from +N).
    idx.Add(baseIndex + 0); idx.Add(baseIndex + 1); idx.Add(baseIndex + 2);
    idx.Add(baseIndex + 0); idx.Add(baseIndex + 2); idx.Add(baseIndex + 3);
}

var cube = Mesh.Create(verts.ToArray(), idx.ToArray());

var window = new Window3D
{
    Title = "Lit cube: directional + ambient",
    BackgroundColor = new Color(8, 8, 24),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 1.5f, 5f),
};

window.Rendering += (w, rd) =>
{
    rd.Camera = camera;

    // Soft ambient so the unlit side isn't pure black; bright
    // directional that orbits the cube so every face gets a turn.
    rd.AmbientLight = new Color(40, 40, 60);

    var t = (float)rd.ElapsedSinceStart.TotalSeconds;
    var lightDir = Vector3.Normalize(new Vector3(
        MathF.Cos(t * 0.5f),
        0.6f,
        MathF.Sin(t * 0.5f)));
    rd.DirectionalLight = new DirectionalLight(lightDir, Color.White);

    var model =
        Matrix4x4.CreateRotationY(t * 0.6f) *
        Matrix4x4.CreateRotationX(t * 0.3f);

    using (rd.PushState())
    {
        rd.CullMode = CullMode.Back;
        rd.DrawMesh(cube, ShaderSets.LitColor, new LitArgs(model));
    }

    w.Invalidate();
};

await window.WaitForCloseAsync();

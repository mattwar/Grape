#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/Skybox.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// A spinning camera inside a procedurally-generated skybox.
//
// A skybox is a cubemap (six square images arranged as the inside of
// a cube) sampled by a 3D direction vector instead of a 2D UV. The
// renderer ships a built-in `ShaderSets.Skybox` and `Camera3D` exposes
// `GetSkyboxViewProjection` -- which strips the camera's translation
// so the sky always stays centred on the player no matter where the
// camera moves.
//
// Each face is a solid colour with a 1-pixel black border so face
// edges and orientation are obvious in the rendered image. The
// camera-facing inner cube (drawn after the skybox, with depth
// writes on) verifies that the skybox renders behind opaque
// geometry without any per-draw depth-state gymnastics: the skybox
// shader forces clip-space Z = W, so the perspective divide pins
// every skybox pixel to depth 1 (the far plane).

using System.Numerics;
using Grape;

const int FaceSize = 128;

var cubemap = Cubemap.Create(
    positiveX: MakeFace(new Color(220,  60,  60)),  // +X right  : red
    negativeX: MakeFace(new Color( 60, 200,  60)),  // -X left   : green
    positiveY: MakeFace(new Color( 80, 130, 230)),  // +Y up     : blue
    negativeY: MakeFace(new Color(230, 200,  60)),  // -Y down   : yellow
    positiveZ: MakeFace(new Color(220,  80, 200)),  // +Z back   : magenta
    negativeZ: MakeFace(new Color( 80, 210, 210))); // -Z front  : cyan

// Unit cube geometry for the skybox. Only positions matter -- the
// skybox shader uses the position itself as the cubemap sample
// direction.
var skyboxVertices = new Vertex3D[]
{
    new(-1f, -1f, -1f),
    new( 1f, -1f, -1f),
    new( 1f,  1f, -1f),
    new(-1f,  1f, -1f),
    new(-1f, -1f,  1f),
    new( 1f, -1f,  1f),
    new( 1f,  1f,  1f),
    new(-1f,  1f,  1f),
};

// Index winding doesn't matter here: the skybox is rendered with
// CullMode.None so we don't have to worry about whether the camera
// is "inside" or "outside" the cube.
var skyboxIndices = new uint[]
{
    4, 5, 6,   4, 6, 7,   // +Z
    1, 0, 3,   1, 3, 2,   // -Z
    0, 4, 7,   0, 7, 3,   // -X
    5, 1, 2,   5, 2, 6,   // +X
    7, 6, 2,   7, 2, 3,   // +Y
    0, 1, 5,   0, 5, 4,   // -Y
};
var skyboxMesh = Mesh.Create(skyboxVertices, skyboxIndices);

// A small inner cube (one solid colour per vertex) so the depth
// interaction with the skybox is obvious.
var innerCubeVertices = new ColorVertex3D[]
{
    new(new Vertex3D(-0.5f, -0.5f, -0.5f), new Color(255, 255, 255)),
    new(new Vertex3D( 0.5f, -0.5f, -0.5f), new Color(220, 220, 220)),
    new(new Vertex3D( 0.5f,  0.5f, -0.5f), new Color(180, 180, 180)),
    new(new Vertex3D(-0.5f,  0.5f, -0.5f), new Color(140, 140, 140)),
    new(new Vertex3D(-0.5f, -0.5f,  0.5f), new Color(100, 100, 100)),
    new(new Vertex3D( 0.5f, -0.5f,  0.5f), new Color( 60,  60,  60)),
    new(new Vertex3D( 0.5f,  0.5f,  0.5f), new Color( 30,  30,  30)),
    new(new Vertex3D(-0.5f,  0.5f,  0.5f), new Color(  8,   8,   8)),
};
var innerCube = Mesh.Create(innerCubeVertices, skyboxIndices);

var window = new Window3D
{
    Title = "Skybox: cubemap-sampled environment",
    BackgroundColor = new Color(0, 0, 0),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera();

window.Rendering += (w, rd) =>
{
    var t = (float)rd.ElapsedSinceStart.TotalSeconds;
    var (width, height) = w.Size;
    var aspect = (float)width / height;

    // Orbit the camera around the origin so every face of the
    // skybox passes through view over time. Radius 3 keeps us
    // comfortably outside the 0.5-unit inner cube at the origin.
    const float OrbitRadius = 3f;
    camera.Position = new Vector3(
        MathF.Sin(t * 0.3f) * OrbitRadius,
        MathF.Sin(t * 0.5f) * 1.2f,
        MathF.Cos(t * 0.3f) * OrbitRadius);
    // Always look at the origin so the inner cube stays in frame
    // while the orbit sweeps the camera through every cubemap face.
    camera.Target = Vector3.Zero;

    // Skybox: translation-stripped view-projection. CullMode.None
    // because the cube is drawn from the inside.
    using (rd.PushState())
    {
        rd.CullMode = CullMode.None;
        rd.DrawMeshRaw(skyboxMesh, cubemap, ShaderSets.Skybox,
            camera.GetSkyboxViewProjection(aspect));
    }

    // Inner cube: regular view-projection (translation kept), with
    // backface culling. Depth writes are on by default so the
    // skybox correctly hides behind the cube where the cube covers
    // it.
    var viewProjection = camera.GetViewProjection(aspect);
    var model =
        Matrix4x4.CreateRotationY(t * 0.7f) *
        Matrix4x4.CreateRotationX(t * 0.4f);
    using (rd.PushState())
    {
        rd.CullMode = CullMode.Back;
        rd.DrawMesh(innerCube, ShaderSets.PositionColorWithTransform,
            model * viewProjection);
    }

    w.Invalidate();
};

await window.WaitForCloseAsync();

static Image MakeFace(Color fill)
{
    var image = Image.Create(FaceSize, FaceSize, PixelFormat.ABGR8888);
    var border = new Color(0, 0, 0);
    for (int y = 0; y < FaceSize; y++)
    {
        for (int x = 0; x < FaceSize; x++)
        {
            // 1px black border + a soft diagonal gradient so the
            // face's orientation (which corner is (0, 0)?) is
            // visually unambiguous.
            bool onBorder = x == 0 || y == 0 || x == FaceSize - 1 || y == FaceSize - 1;
            if (onBorder)
            {
                image.SetPixel(x, y, border);
                continue;
            }
            float k = 0.6f + 0.4f * (1f - (x + y) / (2f * FaceSize));
            image.SetPixel(x, y, new Color(
                (byte)(fill.R * k),
                (byte)(fill.G * k),
                (byte)(fill.B * k)));
        }
    }
    return image;
}

#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/RenderToCubemap.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Render a scene *into* a cubemap, then sample it as a skybox.
//
// Unlike `Skybox.cs` (which fills each face by writing pixels with
// `Image.SetPixel`), this sample uses `Cubemap.RenderAllFaces` to
// run a 3D render pass into every face -- six colored cubes placed
// along the world axes, photographed from the cube's center with a
// 90 degree FOV camera per face. After the bake, the camera orbits
// the origin and the bake result shows up as the skybox.
//
// This is the building block for image-based lighting: replace the
// six colored cubes with a procedural sky / equirect HDR / scene
// render, and you have an environment map for PBR reflections.

using System.Numerics;
using Blitter;
using Blitter.Bits;

const int FaceSize = 256;

// Empty faces -- contents will be replaced by RenderAllFaces.
static Image Blank() => Image.Create(FaceSize, FaceSize, PixelFormat.ABGR8888);
var cubemap = Cubemap.Create(
    Blank(), Blank(), Blank(), Blank(), Blank(), Blank());

// Six colored cubes placed along the world axes. Each cube is large
// enough (size 2 at distance 3, ~37 deg angular size) to dominate
// its face, so a casual glance at the orbiting camera makes it
// obvious which face is which.
const float MarkerDistance = 3f;
const float MarkerSize = 2f;
var cubeMesh = MakeUnitCube(MarkerSize);
var markers = new (Vector3 Position, Color Color)[]
{
    (new Vector3( MarkerDistance,  0,  0), new Color(230,  60,  60)),  // +X bright red
    (new Vector3(-MarkerDistance,  0,  0), new Color(110,  20,  20)),  // -X dark red
    (new Vector3( 0,  MarkerDistance,  0), new Color( 80, 220,  90)),  // +Y bright green
    (new Vector3( 0, -MarkerDistance,  0), new Color( 20, 100,  30)),  // -Y dark green
    (new Vector3( 0,  0,  MarkerDistance), new Color( 80, 130, 230)),  // +Z bright blue
    (new Vector3( 0,  0, -MarkerDistance), new Color( 20,  40, 110)),  // -Z dark blue
};

// Bake all six faces. Camera sits at origin with 90 deg FOV; each
// face's view direction + up vector come from the CubeFace helpers.
var faceCam = new PerspectiveCamera
{
    Position = Vector3.Zero,
    FieldOfView = MathF.PI / 2f,  // 90 degrees: each face covers exactly one cube side
    NearPlane = 0.1f,
    FarPlane = 100f,
};

cubemap.RenderAllFaces(new Color(20, 20, 30), (face, rd) =>
{
    faceCam.Target = face.GetForward();
    faceCam.Up = face.GetUp();
    var vp = faceCam.GetViewProjection(rd.AspectRatio);
    foreach (var (pos, col) in markers)
    {
        var model = Matrix4x4.CreateTranslation(pos);
        rd.DrawMesh(TintMesh(cubeMesh, col), Shaders.PositionColorWithTransform, model * vp);
    }
});

// Now display the baked cubemap as a skybox.
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

var window = new Window3D
{
    Title = "Cubemap.RenderFace: skybox baked from a 3D render pass",
    BackgroundColor = Color.Black,
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera();
await window.RunAsync(rd =>
{
    var t = rd.ElapsedSecondsSinceStart;
    // Sweep the camera through enough latitude that the +Y / -Y
    // faces pass through the centre of the view (otherwise the
    // green cubes sit at the poles and look like distant edge
    // smears). Camera radius is tiny so we're effectively looking
    // outward from the cube's centre.
    const float Radius = 0.1f;
    float yaw = t * 0.4f;
    float pitch = MathF.Sin(t * 0.25f) * (MathF.PI / 2.05f); // -~90 deg .. +~90 deg
    camera.Position = Vector3.Zero;
    camera.Target = new Vector3(
        MathF.Cos(pitch) * MathF.Sin(yaw),
        MathF.Sin(pitch),
        MathF.Cos(pitch) * MathF.Cos(yaw)) * Radius;

    using (rd.PushState())
    {
        rd.CullMode = CullMode.None;
        rd.DrawMeshRaw(skyboxMesh, cubemap, Shaders.Skybox,
            camera.GetSkyboxViewProjection(rd.AspectRatio));
    }
});

static Mesh<ColorVertex3D> MakeUnitCube(float size)
{
    float h = size * 0.5f;
    var v = new Vertex3D[]
    {
        new(-h, -h, -h), new( h, -h, -h),
        new( h,  h, -h), new(-h,  h, -h),
        new(-h, -h,  h), new( h, -h,  h),
        new( h,  h,  h), new(-h,  h,  h),
    };
    var cv = new ColorVertex3D[v.Length];
    for (int i = 0; i < v.Length; i++) cv[i] = new ColorVertex3D(v[i], Color.White);
    var indices = new uint[]
    {
        0, 2, 1,  0, 3, 2,  4, 5, 6,  4, 6, 7,
        0, 1, 5,  0, 5, 4,  3, 7, 6,  3, 6, 2,
        0, 4, 7,  0, 7, 3,  1, 2, 6,  1, 6, 5,
    };
    return Mesh.Create(cv, indices);
}

static Mesh<ColorVertex3D> TintMesh(Mesh<ColorVertex3D> source, Color tint)
{
    // The DrawMesh path with PositionColorWithTransform reads per-vertex
    // color, so build a fresh mesh tinted to the marker color rather
    // than threading a uniform.
    var verts = source.Vertices.ToArray();
    for (int i = 0; i < verts.Length; i++)
        verts[i] = new ColorVertex3D(verts[i].Position, tint);
    return Mesh.Create(verts, source.Indices.ToArray());
}

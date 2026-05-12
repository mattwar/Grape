#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/BlendModes.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// The samples/NuGet.config in this folder pulls Blitter from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.

// Four wide bands stacked vertically with visible gaps between them,
// each demonstrating one BlendMode over a smooth 2D color gradient
// (hue across, brightness top-to-bottom). The gaps let you compare
// the bare backdrop with the band's blended output. Each band is
// labeled in its own region so the connection between band and mode
// is obvious:
//   - Opaque:   replaces destination -- no transparency
//   - Alpha:    standard mix using src alpha
//   - Additive: src is added (great for glow)
//   - Multiply: dst is darkened/tinted
// Bands slide horizontally so each band passes over different hues
// over time. Translucent draws use DepthMode.Transparent so they
// don't occlude each other via the depth buffer. Labels drawn last
// with DepthMode.Overlay so they always read clearly.

using System.Numerics;
using Blitter;
using Blitter.Bits;

// HSV -> RGB; H in [0,1), S/V in [0,1].
static Color Hsv(float h, float s, float v, byte a = 255)
{
    h = h - MathF.Floor(h);
    int i = (int)(h * 6f);
    float f = h * 6f - i;
    float p = v * (1f - s);
    float q = v * (1f - f * s);
    float t = v * (1f - (1f - f) * s);
    (float r, float g, float b) = (i % 6) switch
    {
        0 => (v, t, p),
        1 => (q, v, p),
        2 => (p, v, t),
        3 => (p, q, v),
        4 => (t, p, v),
        _ => (v, p, q),
    };
    return new Color((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), a);
}

// Smooth backdrop: hue cycles across columns, brightness ramps from
// dark at the top to light at the bottom. Vertex colors interpolate
// across each cell so the result is a smooth 2D color field with no
// visible cell boundaries -- this is important so the eye can pick
// out the four blend bands without confusing them with backdrop
// stripes.
static Mesh<ColorVertex3D> MakeBackdrop()
{
    const int Cols = 6;
    const float Left = -2.5f, Right = 2.5f, Top = 1.4f, Bottom = -1.4f;
    float colW = (Right - Left) / Cols;

    // (Cols + 1) vertex columns, two rows (top dark, bottom light).
    var topColors = new Color[Cols + 1];
    var botColors = new Color[Cols + 1];
    for (int i = 0; i <= Cols; i++)
    {
        float h = i / (float)Cols;
        topColors[i] = Hsv(h, 0.7f, 0.20f);
        botColors[i] = Hsv(h, 0.7f, 0.95f);
    }

    var verts = new List<ColorVertex3D>(Cols * 6);
    for (int i = 0; i < Cols; i++)
    {
        float x0 = Left + i * colW;
        float x1 = x0 + colW;
        var pBL = new Vertex3D(x0, Bottom, 0f);
        var pBR = new Vertex3D(x1, Bottom, 0f);
        var pTR = new Vertex3D(x1, Top,    0f);
        var pTL = new Vertex3D(x0, Top,    0f);
        verts.Add(new ColorVertex3D(pBL, botColors[i]));
        verts.Add(new ColorVertex3D(pBR, botColors[i + 1]));
        verts.Add(new ColorVertex3D(pTR, topColors[i + 1]));
        verts.Add(new ColorVertex3D(pBL, botColors[i]));
        verts.Add(new ColorVertex3D(pTR, topColors[i + 1]));
        verts.Add(new ColorVertex3D(pTL, topColors[i]));
    }
    return Mesh.Create([.. verts]);
}

// All four sample bands use the same source color so any visible
// difference between bands is purely the blend mode at work. Mid
// magenta with alpha=180 reads clearly over both bright and dark
// regions of the backdrop and makes each mode's effect distinct:
//   - Opaque:   solid magenta -- backdrop completely hidden
//   - Alpha:    semi-transparent magenta tint
//   - Additive: backdrop brightened toward magenta (glow)
//   - Multiply: backdrop darkened toward magenta
var bandColor = new Color(220, 80, 200, 180);
var opaqueQuad   = Meshes.Rectangle(bandColor);
var alphaQuad    = Meshes.Rectangle(bandColor);
var additiveQuad = Meshes.Rectangle(bandColor);
var multiplyQuad = Meshes.Rectangle(bandColor);
var backdrop     = MakeBackdrop();

var window = new Window3D
{
    Title = "Blend Modes",
    BackgroundColor = Color.Black,
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, 0f, 4f),
};

await window.RunAsync(rd =>
{
    var t = rd.ElapsedSecondsSinceStart;
    var viewProjection = camera.GetViewProjection(rd);

    // Backdrop drawn opaque (default DepthMode.Solid + BlendMode.Alpha
    // is fine here -- the quad's vertices have alpha=255 so it acts
    // opaque and writes depth normally).
    rd.DrawMesh(backdrop, Shaders.PositionColorWithTransform, viewProjection);

    // Four wide thin bands stacked vertically. Bands are well
    // separated by visible gaps so the bare backdrop shows through
    // between them, making each band's region obvious. Each band
    // slides slightly so its mode is shown over different hues over
    // time -- but the band's vertical position is fixed so the label
    // never leaves it.
    float slide = MathF.Sin(t * 0.8f) * 0.4f;
    const float BandWidth = 4.0f;
    const float BandHeight = 0.45f;
    (Mesh<ColorVertex3D> Mesh, BlendMode Mode, string Label, float Y)[] samples =
    [
        (opaqueQuad,   BlendMode.Opaque,   "Opaque",    1.00f),
        (alphaQuad,    BlendMode.Alpha,    "Alpha",     0.34f),
        (additiveQuad, BlendMode.Additive, "Additive", -0.34f),
        (multiplyQuad, BlendMode.Multiply, "Multiply", -1.00f),
    ];

    foreach (var sample in samples)
    {
        var transform = Matrix4x4.CreateScale(BandWidth, BandHeight, 1f) *
                        Matrix4x4.CreateTranslation(slide, sample.Y, 0f) *
                        viewProjection;

        // PushState() so each sample's blend setting is scoped: the
        // Opaque draw doesn't leak into the Alpha draw, etc.
        using (rd.PushState())
        {
            rd.BlendMode = sample.Mode;
            // Bands and backdrop are coplanar at z=0, so use Overlay
            // to bypass depth testing entirely -- otherwise the bands
            // get discarded on the equal-depth comparison and you only
            // see the gradient backdrop.
            rd.DepthMode = DepthMode.Overlay;
            rd.DrawMesh(sample.Mesh, Shaders.PositionColorWithTransform, transform);
        }
    }

    // Labels drawn last in Overlay mode so they always render on top
    // regardless of depth and aren't affected by the per-sample blend
    // state. Centered horizontally inside each band so the connection
    // between band and mode is obvious.
    using (rd.PushState())
    {
        rd.DepthMode = DepthMode.Overlay;
        rd.BlendMode = BlendMode.Alpha;
        foreach (var sample in samples)
            DrawLabel(rd, sample.Label, sample.Y, viewProjection);
    }
});

static void DrawLabel(Renderer3D renderer, string text, float centerY, Matrix4x4 viewProjection)
{
    // Match the centering pattern used by the other 3D samples:
    // shift x by -text.Length/2 in pre-scale units, then scale, then
    // translate to the band's center y.
    const float scale = 0.12f;
    var transform =
        Matrix4x4.CreateTranslation(-text.Length / 2f, -0.5f, 0f) *
        Matrix4x4.CreateScale(scale) *
        Matrix4x4.CreateTranslation(0f, centerY, 0f) *
        viewProjection;
    renderer.DrawDebugText(text, transform);
}

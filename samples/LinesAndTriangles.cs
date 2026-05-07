#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/LinesAndTriangles.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// Three meshes drawn with three different topologies through the same
// renderer:
//
//   * RGB world axes  -> Topology.LineList   (3 line segments)
//   * Animated sine   -> Topology.LineStrip  (one continuous polyline)
//   * Filled triangle -> Topology.TriangleList (default)
//
// The same `RenderMesh` call handles all three -- the topology is a
// property of the mesh, so the renderer compiles a separate pipeline
// for each (cached after first use) and routes the vertex data through
// it correctly.

using System.Numerics;
using Grape;

// World axes: three independent line segments. Each pair of vertices
// in the buffer is one line, so the topology is LineList.
//
//   X axis: red,    from (0,0,0) to (+1,0,0)
//   Y axis: green,  from (0,0,0) to (0,+1,0)
//   Z axis: blue,   from (0,0,0) to (0,0,+1)
var axes = Mesh.Create<ColorVertex3D>(
[
    new(new Vertex3D(0f, 0f, 0f), new Color(255, 64, 64)),
    new(new Vertex3D(1f, 0f, 0f), new Color(255, 64, 64)),

    new(new Vertex3D(0f, 0f, 0f), new Color(64, 255, 64)),
    new(new Vertex3D(0f, 1f, 0f), new Color(64, 255, 64)),

    new(new Vertex3D(0f, 0f, 0f), new Color(64, 128, 255)),
    new(new Vertex3D(0f, 0f, 1f), new Color(64, 128, 255)),
],
    topology: Topology.LineList);

// A filled reference triangle off to the right, rendered with the
// default TriangleList topology. Just to show solid and line-based
// meshes coexist in one frame.
var triangle = Mesh.Create<ColorVertex3D>(
[
    new(new Vertex3D( 0.0f,  0.6f, 0f), new Color(255, 220, 80)),
    new(new Vertex3D(-0.5f, -0.4f, 0f), new Color(80,  220, 255)),
    new(new Vertex3D( 0.5f, -0.4f, 0f), new Color(220, 80,  255)),
]);

// The sine plot is rebuilt every frame: 96 vertices forming one long
// continuous polyline (LineStrip). N vertices = N-1 line segments,
// half the data of a LineList for the same picture.
const int PlotPoints = 96;
const float PlotWidth = 3.0f;
var plotVertices = new ColorVertex3D[PlotPoints];

var plot = Mesh.Create(plotVertices, Topology.LineStrip);

var window = new Window3D
{
    Title = "Topology: LineList axes + LineStrip plot + TriangleList triangle",
    BackgroundColor = new Color(8, 8, 24),
    FullScreen = true,
    CloseKey = Key.Escape,
};

var camera = new PerspectiveCamera
{
    Position = new Vector3(0.8f, 1.0f, 4.5f),
    Target = new Vector3(0f, 0.2f, 0f),
};

window.Rendering += (w, rd) =>
{
    var t = (float)rd.ElapsedSinceStart.TotalSeconds;
    var (width, height) = w.Size;
    var viewProjection = camera.GetViewProjection((float)width / height);

    // Refresh the sine plot in-place. The mesh's Version bumps so the
    // GPU buffer re-uploads automatically.
    for (int i = 0; i < PlotPoints; i++)
    {
        float u = i / (float)(PlotPoints - 1);          // 0..1
        float x = (u - 0.5f) * PlotWidth;
        float y = 0.4f * MathF.Sin(u * MathF.PI * 4f + t * 2f);
        // Color sweeps cyan -> magenta along the strip.
        byte r = (byte)(60 + 195 * u);
        byte g = (byte)(180 - 120 * u);
        byte b = (byte)(220 - 60 * u);
        plotVertices[i] = new ColorVertex3D(new Vertex3D(x, y, 0f), new Color(r, g, b));
    }
    plot.Reset(plotVertices, ReadOnlySpan<uint>.Empty);

    // Axes at the world origin.
    rd.DrawMesh(axes, Shaders.PositionColorWithTransform, viewProjection);

    // Sine plot, lifted slightly so it doesn't sit on the X axis.
    var plotModel = Matrix4x4.CreateTranslation(0f, 0.3f, 0f);
    rd.DrawMesh(plot, Shaders.PositionColorWithTransform, plotModel * viewProjection);

    // Filled triangle, parked above and to the right.
    var triModel =
        Matrix4x4.CreateScale(0.6f) *
        Matrix4x4.CreateTranslation(1.4f, 1.0f, 0f);
    rd.DrawMesh(triangle, Shaders.PositionColorWithTransform, triModel * viewProjection);

    w.Invalidate();
};

await window.WaitForCloseAsync();

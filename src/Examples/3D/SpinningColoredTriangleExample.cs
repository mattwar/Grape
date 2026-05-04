using System.Collections.Immutable;
using System.Numerics;
using Grape;

internal static class SpinningColoredTriangleExample
{
    public static async Task Run()
    {
        // A colored triangle in model space (centered at the origin, ~1 unit tall).
        var triangle = new Mesh<ColorVertex3D>(
            vertices: ImmutableArray.Create(
                new ColorVertex3D(new Vertex3D( 0.0f,  0.5f, 0f), new Color(255, 0,   0)),
                new ColorVertex3D(new Vertex3D( 0.5f, -0.5f, 0f), new Color(0,   255, 0)),
                new ColorVertex3D(new Vertex3D(-0.5f, -0.5f, 0f), new Color(0,   0,   255))),
            indices: ImmutableArray<uint>.Empty);

        var window = new Window3D(800, 600)
        {
            Title = "3D Test",
            BackgroundColor = new Color(0, 0, 32),
        };

        var startTime = DateTime.UtcNow;

        window.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Application.Current.Dispose();
        };

        window.RenderingFrame += (_, renderer) =>
        {
            var seconds = (float)(DateTime.UtcNow - startTime).TotalSeconds;
            var (w, h) = window.Size;
            var aspect = (float)h / w;
            var transform =
                Matrix4x4.CreateRotationZ(seconds) *
                Matrix4x4.CreateScale(0.8f) *
                Matrix4x4.CreateScale(aspect, 1f, 1f);

            renderer.RenderMesh(triangle, Shaders.PositionColorTransform, transform);
        };

        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));
        while (!window.IsDisposed)
        {
            await timer.WaitForNextTickAsync();
            window.Invalidate();
        }
    }
}

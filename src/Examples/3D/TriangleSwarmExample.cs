using System.Collections.Immutable;
using System.Numerics;
using SDL3;
using Grape;

/// <summary>
/// A single immutable triangle mesh drawn many times per frame with
/// different transforms. The vertex data is shared (zero-copy) across all
/// draws via <see cref="ImmutableArray{T}"/>; only the per-instance
/// transform changes from frame to frame.
/// </summary>
internal static class TriangleSwarmExample
{
    public static async Task Run()
    {
        const int Count = 24;

        // One small triangle in model space, shared by every instance.
        var triangle = ImmutableArray.Create(
            new ColorVertex3D(new Vertex3D( 0.0f,  0.12f, 0f), new Color(255,   0,   0)),
            new ColorVertex3D(new Vertex3D( 0.10f, -0.08f, 0f), new Color(  0, 255,   0)),
            new ColorVertex3D(new Vertex3D(-0.10f, -0.08f, 0f), new Color(  0,   0, 255)));

        var window = new Window3D(800, 600)
        {
            Title = "Triangle Swarm",
            BackgroundColor = new Color(8, 0, 24),
            FullScreen = true
        };

        var startTime = DateTime.UtcNow;

        window.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Application.Current.Dispose();
        };

        window.RenderingFrame += (_, renderer) =>
        {
            var t = (float)(DateTime.UtcNow - startTime).TotalSeconds;
            var (w, h) = window.Size;
            var aspect = (float)h / w;
            var aspectScale = Matrix4x4.CreateScale(aspect, 1f, 1f);

            // The orbit ring breathes in and out over time.
            float ring = 0.55f + 0.15f * MathF.Sin(t * 0.7f);

            for (int i = 0; i < Count; i++)
            {
                float phase = (float)i / Count;
                float orbitAngle = phase * MathF.Tau + t * 0.5f;
                float spinAngle  = phase * MathF.Tau + t * 2.5f;
                float bob        = 0.05f * MathF.Sin(t * 1.5f + phase * MathF.Tau * 2f);

                float cx = MathF.Cos(orbitAngle) * ring;
                float cy = MathF.Sin(orbitAngle) * ring + bob;

                // Local spin, then place on the orbit, then aspect-correct.
                var transform =
                    Matrix4x4.CreateRotationZ(spinAngle) *
                    Matrix4x4.CreateTranslation(cx, cy, 0f) *
                    aspectScale;

                // Render same triangle with different transforms many times per frame
                // (no GPU upload cost since the vertex data is shared via ImmutableArray)
                renderer.RenderMesh(triangle, renderer.Shaders.PositionColorTransform, transform);
            }
        };

        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));
        while (!window.IsDisposed)
        {
            await timer.WaitForNextTickAsync();
            window.Invalidate();
        }
    }
}

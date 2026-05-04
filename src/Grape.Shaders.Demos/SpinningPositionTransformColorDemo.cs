using System.Collections.Immutable;
using System.Numerics;
using Grape;

namespace Grape.Shaders.Demos;

/// <summary>
/// Renders a position-only triangle with <see cref="Shaders.PositionTransformColor"/>,
/// supplying the MVP and color through a single per-draw uniform value.
/// Color cycles smoothly each frame to exercise the per-draw uniform path.
/// </summary>
internal static class SpinningPositionTransformColorDemo
{
    public static async Task Run()
    {
        var triangle = new VertexOnlyMesh(
            vertices: ImmutableArray.Create(
                new Vertex3D( 0.0f,  0.5f, 0f),
                new Vertex3D( 0.5f, -0.5f, 0f),
                new Vertex3D(-0.5f, -0.5f, 0f)),
            indices: ImmutableArray<uint>.Empty);

        var shader = Shaders.PositionTransformColor;

        var window = new Window3D(800, 600)
        {
            Title = "Grape.Shaders Demo - PositionTransformColor (per-draw args)",
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

            // Smoothly cycle through hues using offset sines.
            var color = new Vector4(
                0.5f + 0.5f * MathF.Sin(seconds * 1.7f),
                0.5f + 0.5f * MathF.Sin(seconds * 2.3f + 2.0f),
                0.5f + 0.5f * MathF.Sin(seconds * 1.1f + 4.0f),
                1.0f);

            renderer.RenderMesh(triangle, shader, new PositionTransformColorArgs
            {
                Mvp   = transform,
                Color = color,
            });
        };

        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));
        while (!window.IsDisposed)
        {
            await timer.WaitForNextTickAsync();
            window.Invalidate();
        }
    }
}

using System.Collections.Immutable;
using System.Numerics;
using Grape;
using Grape.Shaders;

namespace Grape.Shaders.Demos;

/// <summary>
/// Mirror of the Examples project's spinning colored triangle, but rendered
/// with the runtime-compiled <see cref="Shaders.PositionColorTransform"/>
/// instead of the precompiled <see cref="BuiltInShaders.PositionColorTransform"/>.
/// Proves the IR-built shader survives bind / layout / SPIR-V emission and
/// drives a real GPU pipeline end-to-end.
/// </summary>
internal static class SpinningColoredTriangleDemo
{
    public static async Task Run()
    {
        var triangle = new ColoredMesh(
            vertices: ImmutableArray.Create(
                new ColorVertex3D(new Vertex3D( 0.0f,  0.5f, 0f), new Color(255, 0,   0)),
                new ColorVertex3D(new Vertex3D( 0.5f, -0.5f, 0f), new Color(0,   255, 0)),
                new ColorVertex3D(new Vertex3D(-0.5f, -0.5f, 0f), new Color(0,   0,   255))),
            indices: ImmutableArray<uint>.Empty);

        var positionColorTransform = Shaders.PositionColorTransform;

        var window = new Window3D(800, 600)
        {
            Title = "Grape.Shaders Demo - Compiled PositionColorTransform",
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

            renderer.RenderMesh(triangle, positionColorTransform, transform);
        };

        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));
        while (!window.IsDisposed)
        {
            await timer.WaitForNextTickAsync();
            window.Invalidate();
        }
    }
}

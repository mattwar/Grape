using System.Collections.Immutable;
using System.Numerics;
using SDL3;
using SDL3.Model;

internal static class SpinningTexturedTriangleExample
{
    public static async Task Run()
    {
        // A textured triangle. Position is in NDC; UVs are in [0,1] with
        // (0,0) at the top-left of the texture and (1,1) at the bottom-right.
        var triangle = new TexturedMesh(
            vertices: ImmutableArray.Create(
                new TextureVertex3D(new Vertex3D( 0.0f,  0.5f, 0f), new Vector2(0.5f, 0f)),
                new TextureVertex3D(new Vertex3D( 0.5f, -0.5f, 0f), new Vector2(1f,   1f)),
                new TextureVertex3D(new Vertex3D(-0.5f, -0.5f, 0f), new Vector2(0f,   1f))),
            indices: ImmutableArray<uint>.Empty);

        // Procedurally generate a checkerboard surface so any UV/orientation
        // mistake is visually obvious.
        var checker = CreateCheckerboardSurface(256, 256, cellSize: 32);

        var window = new Window3D(800, 600)
        {
            Title = "Textured Triangle",
            BackgroundColor = new SDL.Color { R = 0, G = 0, B = 32, A = 255 },
        };

        var startTime = DateTime.UtcNow;

        window.KeyDown += (_, e) =>
        {
            if (e.Key == SDL.Keycode.Escape)
                Application.Current.Dispose();
        };

        window.Rendering3D += (_, renderer) =>
        {
            var seconds = (float)(DateTime.UtcNow - startTime).TotalSeconds;
            var (w, h) = window.Size;
            var aspect = (float)h / w;
            var transform =
                Matrix4x4.CreateRotationZ(seconds) *
                Matrix4x4.CreateScale(0.8f) *
                Matrix4x4.CreateScale(aspect, 1f, 1f);

            renderer.RenderTexturedMesh(
                triangle,
                renderer.Shaders.TexturedQuadWithMatrix,
                checker,
                transform: transform);
        };

        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));
        while (!window.IsDisposed)
        {
            await timer.WaitForNextTickAsync();
            window.Invalidate();
        }
    }

    private static Surface CreateCheckerboardSurface(int width, int height, int cellSize)
    {
        var surface = Surface.Create(width, height, SDL.PixelFormat.ABGR8888);
        var dark = new SDL.Color { R = 32, G = 32, B = 32, A = 255 };
        var light = new SDL.Color { R = 220, G = 220, B = 220, A = 255 };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var isDark = ((x / cellSize) + (y / cellSize)) % 2 == 0;
                surface.SetPixel(x, y, isDark ? dark : light);
            }
        }

        return surface;
    }
}

using System.Numerics;
using SDL3;
using SDL3.Model;

/// <summary>
/// Animated horizontal ribbon whose vertical position follows a travelling
/// sine wave. The mesh is regenerated and re-uploaded every frame via
/// <see cref="Mesh{TVertex}.Reset(System.ReadOnlySpan{TVertex}, System.ReadOnlySpan{uint})"/>.
/// </summary>
internal static class AnimatedSineRibbonExample
{
    public static async Task Run()
    {
        const int Segments = 128;
        const float Width = 1.6f;        // total horizontal span in NDC
        const float Thickness = 0.04f;   // ribbon thickness in NDC
        const float Amplitude = 0.35f;
        const float Frequency = 4f;      // wavelengths across the ribbon
        const float Speed = 2f;          // travel speed

        // Two triangles per segment, six vertices each.
        var vertices = new ColorVertex3D[Segments * 6];

        var window = new Window3D(800, 600)
        {
            Title = "Animated Sine Ribbon",
            BackgroundColor = new SDL.Color { R = 0, G = 0, B = 32, A = 255 },
        };

        var startTime = DateTime.UtcNow;

        window.KeyDown += (_, e) =>
        {
            if (e.Key == SDL.Keycode.Escape)
                Application.Current.Dispose();
        };

        window.RenderingFrame += (_, renderer) =>
        {
            var t = (float)(DateTime.UtcNow - startTime).TotalSeconds;

            for (int i = 0; i < Segments; i++)
            {
                float u0 = (float)i / Segments;
                float u1 = (float)(i + 1) / Segments;

                float x0 = -Width / 2f + u0 * Width;
                float x1 = -Width / 2f + u1 * Width;

                float y0 = MathF.Sin(u0 * Frequency * MathF.Tau + t * Speed) * Amplitude;
                float y1 = MathF.Sin(u1 * Frequency * MathF.Tau + t * Speed) * Amplitude;

                var c0 = HsvToColor(u0 + t * 0.1f, 1f, 1f);
                var c1 = HsvToColor(u1 + t * 0.1f, 1f, 1f);

                var topLeft     = new ColorVertex3D(new Vertex3D(x0, y0 + Thickness, 0f), c0);
                var bottomLeft  = new ColorVertex3D(new Vertex3D(x0, y0 - Thickness, 0f), c0);
                var topRight    = new ColorVertex3D(new Vertex3D(x1, y1 + Thickness, 0f), c1);
                var bottomRight = new ColorVertex3D(new Vertex3D(x1, y1 - Thickness, 0f), c1);

                int v = i * 6;
                vertices[v + 0] = topLeft;
                vertices[v + 1] = bottomLeft;
                vertices[v + 2] = bottomRight;
                vertices[v + 3] = topLeft;
                vertices[v + 4] = bottomRight;
                vertices[v + 5] = topRight;
            }

            var (w, h) = window.Size;
            var aspect = (float)h / w;
            var transform = Matrix4x4.CreateScale(aspect, 1f, 1f);

            // reuse same array with different vertex data each frame
            renderer.RenderMesh(vertices, renderer.Shaders.PositionColorTransform, transform);
        };

        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));
        while (!window.IsDisposed)
        {
            await timer.WaitForNextTickAsync();
            window.Invalidate();
        }
    }

    private static SDL.Color HsvToColor(float h, float s, float v)
    {
        h -= MathF.Floor(h);                  // wrap to [0, 1)
        float c = v * s;
        float hh = h * 6f;
        float x = c * (1f - MathF.Abs(hh % 2f - 1f));
        float r, g, b;
        switch ((int)hh)
        {
            case 0: r = c; g = x; b = 0; break;
            case 1: r = x; g = c; b = 0; break;
            case 2: r = 0; g = c; b = x; break;
            case 3: r = 0; g = x; b = c; break;
            case 4: r = x; g = 0; b = c; break;
            default: r = c; g = 0; b = x; break;
        }
        float m = v - c;
        return new SDL.Color
        {
            R = (byte)((r + m) * 255f),
            G = (byte)((g + m) * 255f),
            B = (byte)((b + m) * 255f),
            A = 255,
        };
    }
}

using System.Numerics;
using SDL3;
using Grape;

/// <summary>
/// Demonstrates 3D debug text. The string is updated every frame to show
/// the elapsed time and frame number; the text mesh re-uploads via the
/// renderer's array-keyed mesh cache, sharing one GPU vertex buffer and
/// one GPU font atlas texture across all draws.
/// </summary>
internal static class DebugTextExample
{
    public static async Task Run()
    {
        var window = new Window3D(800, 600)
        {
            Title = "Debug Text",
            BackgroundColor = new Color(16, 0, 32),
        };

        var startTime = DateTime.UtcNow;
        long frame = 0;

        window.KeyDown += (_, e) =>
        {
            if (e.Key == SDL.Keycode.Escape)
                Application.Current.Dispose();
        };

        window.RenderingFrame += (_, renderer) =>
        {
            frame++;
            var t = (float)(DateTime.UtcNow - startTime).TotalSeconds;
            var (w, h) = window.Size;
            var aspect = (float)h / w;

            // Top line: a fixed banner that swings left-to-right with a
            // gentle rotational wobble and slight vertical bob.
            {
                const string banner = "Hello, 3D world!";
                float swing = 0.4f * MathF.Sin(t * 0.8f);
                float bob = 0.04f * MathF.Sin(t * 2.3f);
                float roll = 0.15f * MathF.Sin(t * 1.7f);
                // Scale text to ~0.08 NDC units tall, center-align.
                float scale = 0.08f;
                var transform =
                    // Center the banner around its own origin (glyphs are 1 unit each).
                    Matrix4x4.CreateTranslation(-banner.Length / 2f, -0.5f, 0f) *
                    Matrix4x4.CreateScale(scale) *
                    Matrix4x4.CreateRotationZ(roll) *
                    Matrix4x4.CreateTranslation(swing, 0.4f + bob, 0f) *
                    Matrix4x4.CreateScale(aspect, 1f, 1f);
                renderer.RenderDebugText(banner, transform);
            }

            // Bottom line: live readout that grows/shrinks character count.
            {
                var live = $"t={t:F2}s frame={frame}";
                float scale = 0.06f;
                float widthInNdc = live.Length * scale;
                var transform =
                    Matrix4x4.CreateScale(scale) *
                    Matrix4x4.CreateTranslation(-widthInNdc / 2f, -0.5f, 0f) *
                    Matrix4x4.CreateScale(aspect, 1f, 1f);
                renderer.RenderDebugText(live, transform);
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

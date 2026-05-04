using System.Collections.Immutable;
using System.Numerics;
using Grape;

/// <summary>
/// Exercises every position-only built-in shader in <see cref="Shaders"/>:
/// <see cref="Shaders.Position"/> (white, no transform),
/// <see cref="Shaders.PositionTransform"/> (white, transformed), and
/// <see cref="Shaders.PositionTransformColor"/> (per-draw fragment color,
/// transformed). All three draw the same triangle mesh in different
/// quadrants of the window so the visual result tells you at a glance
/// whether each shader pipeline survives the runtime HLSL -> shadercross
/// path.
/// </summary>
internal static class PositionShadersExample
{
    public static async Task Run()
    {
        // Position-only triangle, ~1 unit tall, centered at origin in model
        // space. The same vertex data drives every shader so any visual
        // difference between quadrants is purely the shader/uniform path.
        var triangle = new VertexOnlyMesh(
            vertices: ImmutableArray.Create(
                new Vertex3D( 0.0f,  0.5f, 0f),
                new Vertex3D( 0.5f, -0.5f, 0f),
                new Vertex3D(-0.5f, -0.5f, 0f)),
            indices: ImmutableArray<uint>.Empty);

        var window = new Window3D(800, 600)
        {
            Title = "Position Shaders",
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

            // Common scale + aspect correction so each triangle sits
            // comfortably inside its quadrant.
            var fit = Matrix4x4.CreateScale(0.35f) * Matrix4x4.CreateScale(aspect, 1f, 1f);
            var spin = Matrix4x4.CreateRotationZ(seconds);

            // Top-left: Position (no transform). The mesh's own vertex
            // coordinates are NDC, so we just shift it manually with a
            // bias-baked-in mesh offset baked into the fit transform's
            // translate, but since Position takes no transform we instead
            // skip this triangle on draw and draw a small static one whose
            // vertices live in the top-left already.
            // To keep the example simple we draw a separate static triangle
            // here whose model-space coordinates are already where we want
            // them on screen.
            renderer.RenderMesh(StaticTopLeft, Shaders.Position);

            // Top-right: PositionTransform (white, spinning, translated).
            var topRight = spin * fit * Matrix4x4.CreateTranslation( 0.5f,  0.5f, 0f);
            renderer.RenderMesh(triangle, Shaders.PositionTransform, topRight);

            // Bottom-left: PositionTransformColor with red.
            var bottomLeft = spin * fit * Matrix4x4.CreateTranslation(-0.5f, -0.5f, 0f);
            renderer.RenderMesh(triangle, Shaders.PositionTransformColor, new PositionTransformColorArgs
            {
                Mvp = bottomLeft,
                Color = new Vector4(1f, 0.2f, 0.2f, 1f),
            });

            // Bottom-right: PositionTransformColor with a hue-cycling color
            // and counter-rotation, proving the per-draw color is genuinely
            // refreshed every frame and not pinned at construction.
            var bottomRight =
                Matrix4x4.CreateRotationZ(-seconds) * fit *
                Matrix4x4.CreateTranslation( 0.5f, -0.5f, 0f);
            renderer.RenderMesh(triangle, Shaders.PositionTransformColor, new PositionTransformColorArgs
            {
                Mvp = bottomRight,
                Color = HueToRgb(seconds * 0.25f),
            });
        };

        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));
        while (!window.IsDisposed)
        {
            await timer.WaitForNextTickAsync();
            window.Invalidate();
        }
    }

    /// <summary>
    /// Converts a hue value in [0,1) (wrapped) at full saturation and value
    /// to an opaque RGBA <see cref="Vector4"/>. Just enough HSV to make the
    /// hue-cycling demo legible; not a general-purpose color util.
    /// </summary>
    private static Vector4 HueToRgb(float hue)
    {
        hue -= MathF.Floor(hue);
        var h6 = hue * 6f;
        var x  = 1f - MathF.Abs((h6 % 2f) - 1f);
        var (r, g, b) = (int)h6 switch
        {
            0 => (1f, x,  0f),
            1 => (x,  1f, 0f),
            2 => (0f, 1f, x),
            3 => (0f, x,  1f),
            4 => (x,  0f, 1f),
            _ => (1f, 0f, x),
        };
        return new Vector4(r, g, b, 1f);
    }

    /// <summary>
    /// A tiny static triangle whose vertex positions are already in NDC
    /// space inside the top-left quadrant. Used to exercise
    /// <see cref="Shaders.Position"/>, which takes no per-draw transform.
    /// </summary>
    private static readonly VertexOnlyMesh StaticTopLeft = new(
        vertices: ImmutableArray.Create(
            new Vertex3D(-0.5f,  0.7f, 0f),
            new Vertex3D(-0.3f,  0.3f, 0f),
            new Vertex3D(-0.7f,  0.3f, 0f)),
        indices: ImmutableArray<uint>.Empty);
}

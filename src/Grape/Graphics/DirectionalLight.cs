using System.Numerics;

namespace Grape;

/// <summary>
/// An infinitely-distant light source that illuminates every point in
/// the scene from the same direction with the same color (think:
/// sunlight). Set on <see cref="Renderer3D.DirectionalLight"/>; lit
/// shaders pick it up automatically through <see cref="IRenderArgs{TSelf}"/>.
/// </summary>
public readonly struct DirectionalLight
{
    /// <summary>
    /// World-space direction pointing <em>from</em> the lit surface
    /// <em>toward</em> the light. The shader normalises this on use,
    /// so callers don't need to pre-normalise.
    /// </summary>
    public Vector3 Direction { get; }

    /// <summary>
    /// Color and intensity of the light. The RGB channels modulate the
    /// surface color; the alpha channel is currently ignored.
    /// </summary>
    public Color Color { get; }

    public DirectionalLight(Vector3 direction, Color color)
    {
        Direction = direction;
        Color = color;
    }
}

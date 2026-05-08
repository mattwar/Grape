using System.Numerics;
using System.Runtime.InteropServices;

namespace Grape;

/// <summary>
/// A point light source: a position in world space that radiates light
/// equally in every direction, fading out smoothly to nothing at
/// <see cref="Range"/>. Add to <see cref="Renderer3D.PointLights"/>; lit
/// shaders pick the whole list up automatically through
/// <see cref="IRenderArgs{TSelf}"/> and the renderer's storage buffer
/// binding.
/// </summary>
/// <remarks>
/// The runtime layout is two <see cref="Vector4"/>s -- 32 bytes -- so the
/// struct can be uploaded straight into a GPU storage buffer that the
/// fragment shader reads as
/// <c>StructuredBuffer&lt;PointLight&gt;</c>. Position and range share the
/// first vec4; color and intensity share the second.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly struct PointLight
{
    private readonly Vector4 _positionRange;   // xyz = position, w = range
    private readonly Vector4 _colorIntensity;  // rgb = color, a = intensity

    /// <summary>World-space position of the light.</summary>
    public Vector3 Position => new(_positionRange.X, _positionRange.Y, _positionRange.Z);

    /// <summary>
    /// Distance at which the light's contribution fades to zero. The
    /// shader uses <c>saturate(1 - distance/range)<sup>2</sup></c> for
    /// attenuation, so the light is at full strength at the source and
    /// hits zero exactly at <see cref="Range"/>.
    /// </summary>
    public float Range => _positionRange.W;

    /// <summary>Light color (RGB; alpha is unused at the source).</summary>
    public Color Color => new(
        (byte)Math.Clamp(_colorIntensity.X * 255f, 0, 255),
        (byte)Math.Clamp(_colorIntensity.Y * 255f, 0, 255),
        (byte)Math.Clamp(_colorIntensity.Z * 255f, 0, 255),
        255);

    /// <summary>
    /// Multiplier on top of <see cref="Color"/>. Lets you scale a light
    /// brighter than 1.0 without leaving the 0..255 color space; the
    /// shader applies it after the per-channel color, so bright lights
    /// can wash a surface to white.
    /// </summary>
    public float Intensity => _colorIntensity.W;

    public PointLight(Vector3 position, Color color, float range, float intensity = 1f)
    {
        _positionRange = new Vector4(position, range);
        Vector4 c = color;
        _colorIntensity = new Vector4(c.X, c.Y, c.Z, intensity);
    }
}

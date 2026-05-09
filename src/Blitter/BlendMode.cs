namespace Blitter;

/// <summary>
/// Controls how a draw's output color combines with the existing
/// pixels in the render target.
/// </summary>
/// <remarks>
/// All translucent modes (<see cref="Alpha"/>, <see cref="Additive"/>,
/// <see cref="Multiply"/>) are order-dependent: draws further back in
/// the scene must be queued before those in front. Pair them with
/// <see cref="DepthMode.Transparent"/> so the translucent draw doesn't
/// write to the depth buffer and occlude things behind it that haven't
/// been drawn yet.
/// </remarks>
public enum BlendMode
{
    /// <summary>
    /// Standard alpha blending: source color is mixed with the
    /// destination using the source's alpha channel.
    /// <c>out = src.rgb * src.a + dst.rgb * (1 - src.a)</c>. The
    /// renderer's initial setting.
    /// </summary>
    Alpha,

    /// <summary>
    /// No blending: the source color completely replaces the
    /// destination. Slightly cheaper on the GPU and avoids the visual
    /// artifacts that translucent blending can produce when draw order
    /// is wrong.
    /// </summary>
    Opaque,

    /// <summary>
    /// Additive blending: the source color is added to the destination,
    /// scaled by the source's alpha. Useful for glow, fire, sparks, and
    /// other emissive effects. Order-independent across additive draws
    /// (sums commute).
    /// </summary>
    Additive,

    /// <summary>
    /// Multiplicative blending: the destination is multiplied by the
    /// source color. Useful for tinting, shadow overlays, or "color
    /// burn" effects.
    /// </summary>
    Multiply,
}

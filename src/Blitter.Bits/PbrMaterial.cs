namespace Blitter.Bits;

/// <summary>
/// A physically-based rendered (PBR) material.
/// </summary>
public sealed class PbrMaterial : Material
{
    /// <summary>
    /// Linear base color tint (RGBA). Multiplied into the base color
    /// texture sample. Default white.
    /// </summary>
    public Color BaseColor { get; init; } = Color.White;

    /// <summary>
    /// Optional base color (albedo) texture. Null is treated as white.
    /// </summary>
    public Texture2D? BaseColorTexture { get; init; }

    /// <summary>
    /// Metalness, 0 = dielectric (plastic / wood / fabric), 1 = metal.
    /// Multiplied with the metallic channel of
    /// <see cref="MetallicRoughnessTexture"/> when one is set.
    /// </summary>
    public float Metallic { get; init; } = 0f;

    /// <summary>
    /// Surface roughness, 0 = mirror, 1 = fully rough. Multiplied with
    /// the roughness channel of <see cref="MetallicRoughnessTexture"/>
    /// when one is set.
    /// </summary>
    public float Roughness { get; init; } = 1f;

    // Texture channel layout (metallic-roughness in G/B, occlusion in
    // R, etc.) is the shader's contract, not the material's; see
    // PbrShaders.LitPbr. A null texture is treated as opaque white, so
    // missing slots leave the scalar factors unchanged.

    /// <summary>
    /// Optional metallic-roughness texture. Null is treated as white.
    /// </summary>
    public Texture2D? MetallicRoughnessTexture { get; init; }

    /// <summary>
    /// Self-illumination color added on top of the lit result. Default
    /// black (no emission).
    /// </summary>
    public Color Emissive { get; init; } = Color.Black;

    /// <summary>
    /// Optional emissive texture. Null is treated as white so the
    /// emissive factor passes through unchanged.
    /// </summary>
    public Texture2D? EmissiveTexture { get; init; }

    /// <summary>
    /// Strength of ambient occlusion, 0 = none, 1 = full. Scales the
    /// contribution of <see cref="OcclusionTexture"/>.
    /// </summary>
    public float OcclusionStrength { get; init; } = 1f;

    /// <summary>
    /// Optional ambient-occlusion texture. Null is treated as white
    /// (no occlusion).
    /// </summary>
    public Texture2D? OcclusionTexture { get; init; }

    /// <summary>
    /// Featureless white dielectric, fully rough. Useful as a default.
    /// </summary>
    public static PbrMaterial Default { get; } = new();

    /// <summary>
    /// A solid-color metal with the given base color and roughness.
    /// </summary>
    public static PbrMaterial Metal(Color baseColor, float roughness = 0.4f) =>
        new() { BaseColor = baseColor, Metallic = 1f, Roughness = roughness };

    /// <summary>
    /// A solid-color dielectric (plastic / paint) with the given base
    /// color and roughness.
    /// </summary>
    public static PbrMaterial Dielectric(Color baseColor, float roughness = 0.5f) =>
        new() { BaseColor = baseColor, Metallic = 0f, Roughness = roughness };
}

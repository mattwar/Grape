namespace Blitter.Bits;

/// <summary>
/// Visual properties of a surface, separate from its geometry.
/// </summary>
public abstract class Material
{
    /// <summary>
    /// Name of the material, if any.
    /// </summary>
    public string? Name { get; init; }
}

/// <summary>
/// A flat-shaded, optionally textured surface.
/// </summary>
public sealed class LitTextureMaterial : Material
{
    /// <summary>
    /// Flat surface tint, default white (no tinting). Multiplied into
    /// the texture sample at fragment time, so a non-white color with
    /// no texture still produces a colored surface.
    /// </summary>
    public Color DiffuseColor { get; init; } = Color.White;

    /// <summary>
    /// Optional 2D image sampled across the mesh's UVs. 
    /// Null is treated as "no texture".
    /// </summary>
    public Texture2D? DiffuseTexture { get; init; }

    /// <summary>
    /// A featureless white material. Useful as a default.
    /// </summary>
    public static LitTextureMaterial Default { get; } = new();
}


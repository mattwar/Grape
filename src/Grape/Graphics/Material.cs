namespace Grape;

/// <summary>
/// Visual properties of a surface, separate from its geometry.
/// Conceptually a "skin" -- swap one in to repaint the same vertices.
/// Today this only covers a flat diffuse color and an optional diffuse
/// texture, which together drive <see cref="Shaders.LitTexture"/>.
/// </summary>
public sealed class Material
{
    /// <summary>
    /// Flat surface tint, default white (no tinting). Multiplied into
    /// the texture sample at fragment time, so a non-white color with
    /// no texture still produces a colored surface.
    /// </summary>
    public Color DiffuseColor { get; init; } = Color.White;

    /// <summary>
    /// Optional 2D image sampled across the mesh's UVs. Null is treated
    /// as "no texture": the renderer substitutes a 1×1 white texture so
    /// the same shader still binds.
    /// </summary>
    public Image? DiffuseTexture { get; init; }

    /// <summary>
    /// Optional human-readable name -- typically the material's name as
    /// declared in the source file (e.g. an OBJ <c>newmtl</c> entry).
    /// Used for debugging and for callers who want to find a specific
    /// material in a loaded model.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>A featureless white material. Useful as a default.</summary>
    public static Material Default { get; } = new();
}

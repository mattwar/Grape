namespace Blitter.Bits;

/// <summary>
/// Catalog of process-shared images that PBR and other shaders pull
/// from. Each property lazy-initializes on first access and lives for
/// the rest of the process; callers must not <see cref="Image.Dispose"/>
/// the returned images.
/// </summary>
public static class Textures
{
    private static Image? s_white;
    private static Image? s_black;
    private static Image? s_specularLut;

    /// <summary>
    /// 1×1 opaque white image. Use as a placeholder when a shader
    /// expects a texture but the material has none -- the shader's
    /// per-channel factor then passes through unchanged.
    /// </summary>
    public static Image White => s_white ??= CreateSolid(Color.White);

    /// <summary>
    /// 1×1 opaque black image. Use as a placeholder for additive
    /// texture slots (emissive, etc.) so the shader's contribution
    /// reduces to zero when no texture is supplied.
    /// </summary>
    public static Image Black => s_black ??= CreateSolid(Color.Black);

    /// <summary>
    /// 256×256 precomputed split-sum BRDF integration texture used by
    /// PBR specular image-based lighting. R = scale, G = bias for the
    /// Fresnel/visibility term; sample with U = NdotV, V = roughness.
    /// </summary>
    public static Image SpecularLut => s_specularLut ??= EnvironmentBaker.BakeSpecularLut();

    private static Image CreateSolid(Color color)
    {
        var image = Image.Create(1, 1, PixelFormat.ABGR8888);
        image.SetPixel(0, 0, color);
        return image;
    }
}

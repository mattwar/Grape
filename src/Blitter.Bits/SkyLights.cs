namespace Blitter.Bits;

/// <summary>
/// Built-in <see cref="SkyLight"/> instances for image-based lighting.
/// Assign one to <see cref="Renderer3D"/>'s <c>SkyLight</c> extension
/// property before drawing any material (e.g. <see cref="PbrMaterial"/>) that consumes IBL.
/// </summary>
public static class SkyLights
{
    private static SkyLight? s_sky;
    private static SkyLight? s_skySunless;
    private static SkyLight? s_skyFlat;
    private static SkyLight? s_none;

    /// <summary>
    /// Default sky environment: procedural day-sky cubemap baked into
    /// irradiance and roughness-prefiltered specular cubes, combined
    /// with the split-sum BRDF LUT. Lazily built on first access and
    /// cached for the process lifetime.
    /// </summary>
    public static SkyLight Sun => s_sky ??= new()
    {
        Irradiance = Cubemaps.SkyIrradiance,
        Prefiltered = Cubemaps.SkyPrefiltered,
        SpecularLut = Textures.SpecularLut,
    };

    /// <summary>
    /// Sun-less variant of <see cref="Sun"/>: same sky tint, no sun
    /// disc. Use when a directional light is the actual sun and you
    /// don't want a second specular highlight reflected from the sky.
    /// </summary>
    public static SkyLight Sunless => s_skySunless ??= new()
    {
        Irradiance = Cubemaps.SkySunlessIrradiance,
        Prefiltered = Cubemaps.SkySunlessPrefiltered,
        SpecularLut = Textures.SpecularLut,
    };

    /// <summary>
    /// Flat-tint environment: <see cref="Cubemaps.SkyFlat"/> with no
    /// horizon band or sun, so shiny surfaces reflect a uniform tone.
    /// Useful as a neutral IBL baseline for material previews.
    /// </summary>
    public static SkyLight Flat => s_skyFlat ??= new()
    {
        Irradiance = Cubemaps.SkyFlatIrradiance,
        Prefiltered = Cubemaps.SkyFlatPrefiltered,
        SpecularLut = Textures.SpecularLut,
    };

    /// <summary>
    /// Zero-energy environment: black irradiance and prefiltered
    /// cubes, so the IBL term multiplies out to zero and PBR
    /// materials are lit purely by direct lighting (ambient +
    /// directional + point). Used as the default when
    /// <c>Renderer3D.SkyLight</c> is unset so the first PBR draw
    /// renders without forcing the caller to pick a sky.
    /// </summary>
    public static SkyLight None => s_none ??= new()
    {
        Irradiance = Cubemaps.Black,
        Prefiltered = Cubemaps.Black,
        SpecularLut = Textures.White,
    };
}

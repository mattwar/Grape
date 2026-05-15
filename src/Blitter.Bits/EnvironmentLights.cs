namespace Blitter.Bits;

/// <summary>
/// Built-in <see cref="EnvironmentLight"/> instances for image-based lighting.
/// Assign one to <see cref="Renderer3D"/>'s <c>EnvironmentLight</c> extension
/// property before drawing any material (e.g. <see cref="PbrMaterial"/>) that consumes IBL.
/// </summary>
public static class EnvironmentLights
{
    private static EnvironmentLight? s_sky;
    private static EnvironmentLight? s_skySunless;
    private static EnvironmentLight? s_skyFlat;

    /// <summary>
    /// Default sky environment: procedural day-sky cubemap baked into
    /// irradiance and roughness-prefiltered specular cubes, combined
    /// with the split-sum BRDF LUT. Lazily built on first access and
    /// cached for the process lifetime.
    /// </summary>
    public static EnvironmentLight Sky => s_sky ??= new()
    {
        Irradiance = Cubemaps.SkyIrradiance,
        Prefiltered = Cubemaps.SkyPrefiltered,
        SpecularLut = Textures.SpecularLut,
    };

    /// <summary>
    /// Sun-less variant of <see cref="Sky"/>: same sky tint, no sun
    /// disc. Use when a directional light is the actual sun and you
    /// don't want a second specular highlight reflected from the sky.
    /// </summary>
    public static EnvironmentLight SkySunless => s_skySunless ??= new()
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
    public static EnvironmentLight SkyFlat => s_skyFlat ??= new()
    {
        Irradiance = Cubemaps.SkyFlatIrradiance,
        Prefiltered = Cubemaps.SkyFlatPrefiltered,
        SpecularLut = Textures.SpecularLut,
    };
}

namespace Blitter.Bits;

/// <summary>
/// Built-in <see cref="Environment3D"/> instances for image-based lighting.
/// Assign one to <see cref="Renderer3D.Environment"/> before drawing
/// any material (e.g. <see cref="PbrMaterial"/>) that consumes IBL.
/// </summary>
public static class EnvironmentMaps
{
    private static Environment3D? s_sky;

    /// <summary>
    /// Default sky environment: procedural day-sky cubemap baked into
    /// irradiance and roughness-prefiltered specular cubes, combined
    /// with the split-sum BRDF LUT. Lazily built on first access and
    /// cached for the process lifetime.
    /// </summary>
    public static Environment3D Sky => s_sky ??= new()
    {
        Irradiance = Cubemaps.SkyIrradiance,
        Prefiltered = Cubemaps.SkyPrefiltered,
        SpecularLut = Textures.SpecularLut,
    };
}

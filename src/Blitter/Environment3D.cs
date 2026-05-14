namespace Blitter;

/// <summary>
/// The lighting surrounding a 3D scene, used to shade reflective and shiny surfaces. 
/// Assign to <see cref="Renderer3D.Environment"/>.
/// </summary>
public sealed class Environment3D
{
    /// <summary>
    /// A cubemap that stores incoming diffuse light from every direction.
    /// </summary>
    public required Cubemap Irradiance { get; init; }

    /// <summary>
    /// A cubemap that stores the environment as a mirror sees it, with each mip pre-blurred for a different surface roughness.
    /// </summary>
    public required Cubemap Prefiltered { get; init; }

    /// <summary>
    /// A texture that stores how strongly a surface reflects light, indexed by view angle and roughness.
    /// </summary>
    public required Image SpecularLut { get; init; }
}

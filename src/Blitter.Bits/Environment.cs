using System.Runtime.CompilerServices;
using Blitter;

namespace Blitter.Bits;

/// <summary>
/// The lighting surrounding a 3D scene, used to shade reflective and shiny surfaces.
/// Assign to <see cref="Renderer3D"/>'s <c>Environment</c> extension property.
/// </summary>
public sealed record Environment
{
    /// <summary>
    /// A cubemap that stores incoming diffuse light from every direction.
    /// </summary>
    public required CubeTexture Irradiance { get; init; }

    /// <summary>
    /// A cubemap that stores the environment as a mirror sees it, with each mip pre-blurred for a different surface roughness.
    /// </summary>
    public required CubeTexture Prefiltered { get; init; }

    /// <summary>
    /// A texture that stores how strongly a surface reflects light, indexed by view angle and roughness.
    /// </summary>
    public required Image SpecularLut { get; init; }

    /// <summary>
    /// Rotation of the environment around the world Y axis, in radians.
    /// Useful for moving the sun's apparent position without regenerating the cubemaps.
    /// </summary>
    public float Yaw { get; init; }
}

/// <summary>
/// Adds the <c>Environment</c> extension property to <see cref="Renderer3D"/>.
/// </summary>
public static class Renderer3DEnvironmentExtensions
{
    extension(Renderer3D renderer)
    {
        /// <summary>
        /// Scene-wide image-based-lighting environment. Materializers
        /// (e.g. <see cref="StandardMaterializer"/>) read this when drawing
        /// PBR materials. Stored in a <see cref="ConditionalWeakTable{TKey, TValue}"/>
        /// so the renderer itself doesn't have to know about IBL.
        /// </summary>
        public Environment? Environment
        {
            get => EnvironmentStorage.Table.TryGetValue(renderer, out var env) ? env : null;
            set
            {
                if (value is null)
                {
                    EnvironmentStorage.Table.Remove(renderer);
                }
                else
                {
                    EnvironmentStorage.Table.AddOrUpdate(renderer, value);
                }
            }
        }
    }
}

file static class EnvironmentStorage
{
    public static readonly ConditionalWeakTable<Renderer3D, Environment> Table = new();
}

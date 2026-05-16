using System.Runtime.CompilerServices;
using Blitter;

namespace Blitter.Bits;

/// <summary>
/// The lighting surrounding a 3D scene, used to shade reflective and shiny surfaces.
/// Assign to <see cref="Renderer3D"/>'s <c>SkyLight</c> extension property.
/// </summary>
public sealed record SkyLight
{
    /// <summary>
    /// A cubemap of incoming light from every direction. 
    /// Used to shade matte surfaces -- the soft, ambient tint a non-shiny surface
    /// picks up from its surroundings.
    /// </summary>
    public required TextureCube Diffuse { get; init; }

    /// <summary>
    /// A cubemap of the environment as seen by a mirror.
    /// Used to shade shiny surfaces -- sharp reflections for polished materials,
    /// blurred reflections for rough ones.
    /// </summary>
    public required TextureCube Specular { get; init; }

    /// <summary>
    /// A texture that stores how strongly a surface reflects light, indexed by view angle and roughness.
    /// </summary>
    public required Texture2D SpecularLut { get; init; }

    /// <summary>
    /// Rotation of the environment around the world Y axis, in radians.
    /// Useful for moving the sun's apparent position without regenerating the cubemaps.
    /// </summary>
    public float Yaw { get; init; }
}

/// <summary>
/// Adds the <c>SkyLight</c> extension property to <see cref="Renderer3D"/>.
/// </summary>
public static class Renderer3DSkyLightExtensions
{
    extension(Renderer3D renderer)
    {
        /// <summary>
        /// Scene-wide image-based light. Materializers (e.g.
        /// <see cref="StandardMaterializer"/>) read this when drawing
        /// PBR materials. Stored in a <see cref="ConditionalWeakTable{TKey, TValue}"/>
        /// so the renderer itself doesn't have to know about IBL.
        /// </summary>
        public SkyLight? SkyLight
        {
            get => SkyLightStorage.Table.TryGetValue(renderer, out var env) ? env : null;
            set
            {
                if (value is null)
                {
                    SkyLightStorage.Table.Remove(renderer);
                }
                else
                {
                    SkyLightStorage.Table.AddOrUpdate(renderer, value);
                }
            }
        }
    }
}

file static class SkyLightStorage
{
    public static readonly ConditionalWeakTable<Renderer3D, SkyLight> Table = new();
}

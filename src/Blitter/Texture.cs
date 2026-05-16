namespace Blitter;

/// <summary>
/// Base type for any GPU-samplable texture asset. Sealed to its two
/// engine-provided subtypes -- <see cref="Texture2D"/> (1D or 2D, any
/// dimension that samples by 2D UV) and <see cref="Cubemap"/>
/// (six faces sampled by 3D direction) -- because the renderer's
/// upload and bind paths pattern-match on exactly these kinds.
/// External code never subclasses <c>Texture</c>; it uses one of the
/// concrete types directly. The base exists so the multi-texture
/// <c>DrawMesh</c> overload can take a heterogeneous span of 2D and
/// cubemap textures.
/// </summary>
public abstract class Texture
{
    // Internal constructor: only Image and Cubemap (this assembly)
    // can subclass. The renderer's pattern matches assume the closed
    // set; opening it would silently bypass the upload/bind paths.
    internal Texture() { }
}

namespace Grape;

/// <summary>
/// Controls which triangles are skipped during rasterization based on which
/// way they face relative to the camera. By default Grape uses
/// counter-clockwise winding to identify front faces.
/// </summary>
public enum CullMode
{
    /// <summary>
    /// Draw both sides of every triangle. This is the default and the
    /// safest choice for hand-built geometry where winding may be
    /// inconsistent or both sides are intended to be visible.
    /// </summary>
    None,

    /// <summary>
    /// Skip triangles facing away from the camera. The standard choice
    /// for closed solid meshes (cubes, spheres, etc.) -- back faces are
    /// hidden inside the mesh anyway, so drawing them is wasted work.
    /// Requires consistently wound (counter-clockwise) geometry.
    /// </summary>
    Back,

    /// <summary>
    /// Skip triangles facing the camera. Niche; useful for some
    /// shadow-volume and inside-out rendering tricks.
    /// </summary>
    Front,
}

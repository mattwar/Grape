using System.Numerics;

namespace Blitter;

/// <summary>
/// Identifies one of the six faces of a <see cref="Cubemap"/>. The
/// name says which world-space axis the face is the +/-x/y/z wall
/// of, looking outward from the cube's center.
/// </summary>
public enum CubeFace
{
    /// <summary>The +X face: looking toward world +X (right).</summary>
    PositiveX,
    /// <summary>The -X face: looking toward world -X (left).</summary>
    NegativeX,
    /// <summary>The +Y face: looking toward world +Y (up).</summary>
    PositiveY,
    /// <summary>The -Y face: looking toward world -Y (down).</summary>
    NegativeY,
    /// <summary>The +Z face: looking toward world +Z.</summary>
    PositiveZ,
    /// <summary>The -Z face: looking toward world -Z.</summary>
    NegativeZ,
}

/// <summary>
/// Helpers for using <see cref="CubeFace"/> values to drive cubemap
/// bakes: per-face view direction, image-up vector, and the canonical
/// six-face ordering matching the SDL_GPU / D3D / Vulkan layer order.
/// </summary>
public static class CubeFaceExtensions
{
    // Canonical face ordering: +X, -X, +Y, -Y, +Z, -Z. Matches the
    // SDL_GPU cube layer index used by Renderer.UploadCubemapFaces.
    private static readonly CubeFace[] s_all =
    {
        CubeFace.PositiveX, CubeFace.NegativeX,
        CubeFace.PositiveY, CubeFace.NegativeY,
        CubeFace.PositiveZ, CubeFace.NegativeZ,
    };

    /// <summary>
    /// The six faces in cube-layer order (+X, -X, +Y, -Y, +Z, -Z).
    /// </summary>
    public static IReadOnlyList<CubeFace> All => s_all;

    /// <summary>
    /// Unit vector pointing along the face's outward axis -- use as
    /// the <c>Target</c> of a camera positioned at the cube's center.
    /// </summary>
    public static Vector3 GetForward(this CubeFace face) => face switch
    {
        CubeFace.PositiveX => new Vector3(1, 0, 0),
        CubeFace.NegativeX => new Vector3(-1, 0, 0),
        CubeFace.PositiveY => new Vector3(0, 1, 0),
        CubeFace.NegativeY => new Vector3(0, -1, 0),
        CubeFace.PositiveZ => new Vector3(0, 0, 1),
        CubeFace.NegativeZ => new Vector3(0, 0, -1),
        _ => throw new ArgumentOutOfRangeException(nameof(face), face, null),
    };

    /// <summary>
    /// Unit "up" vector chosen so that the rendered face image
    /// matches the standard D3D / Vulkan cubemap orientation -- pixel
    /// (0, 0) is the upper-left corner as seen from the cube's center
    /// looking outward through that face. Pass to a camera as
    /// <c>Up</c>.
    /// </summary>
    public static Vector3 GetUp(this CubeFace face) => face switch
    {
        // Top of +Y face is the row furthest in -Z (so when you tilt
        // up from looking along +Z, "ahead" ends up at the bottom of
        // the image). Symmetric for -Y.
        CubeFace.PositiveY => new Vector3(0, 0, -1),
        CubeFace.NegativeY => new Vector3(0, 0, 1),
        _ => new Vector3(0, 1, 0),
    };
}

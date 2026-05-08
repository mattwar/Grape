namespace Grape;

/// <summary>
/// A right-angle rotation applied when transforming an image. Angles
/// are measured clockwise as seen by a viewer facing the image
/// (i.e. with the +X axis going right and the +Y axis going down,
/// matching how images are stored in memory).
/// </summary>
public enum Rotation
{
    /// <summary>No rotation; the image is returned unchanged.</summary>
    None,
    /// <summary>Rotate 90 degrees clockwise.</summary>
    Clockwise90,
    /// <summary>Rotate 180 degrees (equivalent to flipping both axes).</summary>
    Half,
    /// <summary>Rotate 90 degrees counter-clockwise (equivalently 270 degrees clockwise).</summary>
    Counterclockwise90,
}

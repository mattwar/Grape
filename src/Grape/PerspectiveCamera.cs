using System.Numerics;

namespace Grape;

/// <summary>
/// A camera that projects 3D world-space points onto the screen with
/// perspective foreshortening: things farther from the camera appear
/// smaller. The right choice for most 3D scenes.
/// </summary>
public sealed class PerspectiveCamera : Camera
{
    /// <summary>
    /// Vertical field of view, in radians. Defaults to 45° (<c>π/4</c>).
    /// Larger values give a wider, more fish-eye-like view.
    /// </summary>
    public float FieldOfView { get; set; } = MathF.PI / 4f;

    /// <summary>
    /// Distance to the near clipping plane. Geometry closer than this
    /// is not drawn. Pushing this value too small (e.g. 0.001f) hurts
    /// depth-buffer precision; defaults to <c>0.1</c>.
    /// </summary>
    public float NearPlane { get; set; } = 0.1f;

    /// <summary>
    /// Distance to the far clipping plane. Geometry farther than this
    /// is not drawn. Defaults to <c>100</c>.
    /// </summary>
    public float FarPlane { get; set; } = 100f;

    public override Matrix4x4 GetProjection(float aspectRatio) =>
        Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, aspectRatio, NearPlane, FarPlane);
}

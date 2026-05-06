using System.Numerics;

namespace Grape;

/// <summary>
/// A camera that projects 3D world-space points onto the screen with
/// no perspective foreshortening: parallel lines stay parallel and an
/// object's on-screen size is independent of its distance from the
/// camera. Common for 2D-in-3D scenes, technical/CAD views, and
/// isometric or top-down games.
/// </summary>
public sealed class OrthographicCamera : Camera3D
{
    /// <summary>
    /// Vertical extent of the view volume in world units. Defaults to
    /// <c>2</c>. The horizontal extent is derived from the aspect ratio
    /// passed to <see cref="Camera3D.GetProjection"/>.
    /// </summary>
    public float Height { get; set; } = 2f;

    /// <summary>
    /// Distance to the near clipping plane. Geometry closer than this
    /// is not drawn. Defaults to <c>0.1</c>.
    /// </summary>
    public float NearPlane { get; set; } = 0.1f;

    /// <summary>
    /// Distance to the far clipping plane. Geometry farther than this
    /// is not drawn. Defaults to <c>100</c>.
    /// </summary>
    public float FarPlane { get; set; } = 100f;

    public override Matrix4x4 GetProjection(float aspectRatio) =>
        Matrix4x4.CreateOrthographic(Height * aspectRatio, Height, NearPlane, FarPlane);
}

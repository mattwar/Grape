using System.Numerics;

namespace Grape;

/// <summary>
/// A camera describes how 3D world-space points should be projected
/// onto the 2D screen. It produces a view matrix (where the camera is
/// and what it's looking at) and, via a subclass, a projection matrix
/// (perspective or orthographic).
/// </summary>
/// <remarks>
/// <para>
/// Mental model: a telescope with two lenses. The view matrix is the
/// objective lens that aims at the scene; the projection matrix is the
/// eyepiece that focuses what was gathered into the shape of the
/// screen. Looking through the assembled telescope -- the combined
/// view-projection matrix -- is what produces the picture you see.
/// </para>
/// <para>
/// A camera is a CPU-side helper -- it doesn't interact with the GPU
/// directly. Sample code typically calls <see cref="GetViewProjection"/>
/// each frame and passes the resulting matrix to a shader that accepts
/// a transform. <see cref="GetView"/> and <see cref="GetProjection"/>
/// are exposed for advanced cases that need to operate between the two
/// stages (e.g. view-space lighting, frustum extraction, picking).
/// </para>
/// </remarks>
public abstract class Camera3D
{
    /// <summary>
    /// World-space position the camera is looking from.
    /// Defaults to <c>(0, 0, 5)</c>.
    /// </summary>
    public Vector3 Position { get; set; } = new(0f, 0f, 5f);

    /// <summary>
    /// World-space point the camera is aimed at.
    /// Defaults to the origin <c>(0, 0, 0)</c>.
    /// </summary>
    public Vector3 Target { get; set; } = Vector3.Zero;

    /// <summary>
    /// Direction the camera considers "up". Defaults to <see cref="Vector3.UnitY"/>
    /// (world's +Y axis), which is the right answer for most scenes.
    /// </summary>
    public Vector3 Up { get; set; } = Vector3.UnitY;

    /// <summary>
    /// Builds the view matrix -- the "objective lens" that aims the
    /// camera at the scene. Derived from <see cref="Position"/>,
    /// <see cref="Target"/>, and <see cref="Up"/>; does not flatten
    /// 3D into 2D.
    /// </summary>
    public Matrix4x4 GetView() =>
        Matrix4x4.CreateLookAt(Position, Target, Up);

    /// <summary>
    /// Builds the projection matrix -- the "eyepiece" that focuses the
    /// camera's view into the shape of the screen, for the given aspect
    /// ratio (width / height). This is the stage that actually flattens
    /// 3D into 2D.
    /// </summary>
    public abstract Matrix4x4 GetProjection(float aspectRatio);

    /// <summary>
    /// The whole camera/telescope: returns <c>view * projection</c>, the
    /// matrix you usually compose with a per-mesh model matrix to
    /// produce the final MVP. This is what almost all rendering code wants.
    /// </summary>
    public Matrix4x4 GetViewProjection(float aspectRatio) =>
        GetView() * GetProjection(aspectRatio);

    /// <summary>
    /// View-projection for drawing a skybox: the same as
    /// <see cref="GetViewProjection"/> but with the camera's
    /// translation stripped out so the skybox stays centred on the
    /// camera regardless of where the camera moves. Pair with
    /// <see cref="Shaders.Skybox"/> and a unit cube mesh.
    /// </summary>
    public Matrix4x4 GetSkyboxViewProjection(float aspectRatio)
    {
        // Zero the translation row of the view matrix. The result is
        // the same orientation as GetView() but anchored at the origin
        // -- so the cube the skybox is drawn on is always centred on
        // the camera and the player can never "reach the edge" of the
        // sky.
        var view = GetView();
        view.M41 = 0f;
        view.M42 = 0f;
        view.M43 = 0f;
        return view * GetProjection(aspectRatio);
    }
}

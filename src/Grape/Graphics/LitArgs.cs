using System.Numerics;
using System.Runtime.InteropServices;

namespace Grape;

/// <summary>
/// Per-draw arguments for <see cref="Shaders.LitColor"/>. Carries the
/// model matrix the user supplies and four lighting fields that the
/// renderer fills in from <see cref="Renderer3D.Camera"/>,
/// <see cref="Renderer3D.AmbientLight"/>, and
/// <see cref="Renderer3D.DirectionalLight"/> via <see cref="IRenderArgs{TSelf}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Model and view-projection are kept as separate fields rather than
/// pre-multiplied so the vertex shader can transform normals by the
/// model matrix alone (assumes the model is rotation + uniform scale +
/// translation; non-uniform scales would need a true inverse-transpose).
/// </para>
/// <para>
/// Default-constructed <see cref="LitArgs"/> has <see cref="Model"/> =
/// identity and zeroed lighting fields; populate <see cref="Model"/>
/// and let the renderer fill in the rest.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct LitArgs : IRenderArgs<LitArgs>
{
    /// <summary>Per-draw world transform. Caller-supplied.</summary>
    public Matrix4x4 Model;

    /// <summary>Camera view-projection. Filled in by the renderer.</summary>
    public Matrix4x4 ViewProjection;

    /// <summary>Ambient color (RGBA, 0..1). Filled in by the renderer.</summary>
    public Vector4 AmbientLight;

    /// <summary>
    /// Directional light direction in world space (xyz; w unused). Filled
    /// in by the renderer; zero means no directional contribution.
    /// </summary>
    public Vector4 LightDirection;

    /// <summary>Directional light color (RGBA, 0..1). Filled in by the renderer.</summary>
    public Vector4 LightColor;

    public LitArgs(Matrix4x4 model)
    {
        Model = model;
        ViewProjection = Matrix4x4.Identity;
        AmbientLight = Vector4.Zero;
        LightDirection = Vector4.Zero;
        LightColor = Vector4.Zero;
    }

    public static implicit operator LitArgs(Matrix4x4 model) => new(model);

    /// <inheritdoc cref="IRenderArgs{TSelf}.SetViewProjection"/>
    public static Func<LitArgs, Matrix4x4, LitArgs>? SetViewProjection { get; } =
        (a, vp) => { a.ViewProjection = vp; return a; };

    /// <inheritdoc cref="IRenderArgs{TSelf}.SetAmbientLight"/>
    public static Func<LitArgs, Vector4, LitArgs>? SetAmbientLight { get; } =
        (a, amb) => { a.AmbientLight = amb; return a; };

    /// <inheritdoc cref="IRenderArgs{TSelf}.SetDirectionalLight"/>
    public static Func<LitArgs, DirectionalLight, LitArgs>? SetDirectionalLight { get; } =
        (a, light) =>
        {
            a.LightDirection = new Vector4(light.Direction, 0f);
            a.LightColor = light.Color;
            return a;
        };
}

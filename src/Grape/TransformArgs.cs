using System.Numerics;
using System.Runtime.InteropServices;

namespace Grape;

/// <summary>
/// A one-field args struct whose only payload is a 4x4 transform
/// matrix, exposed through <see cref="IUniformArgs{TSelf}"/> so
/// the renderer can compose its <see cref="Renderer3D.Camera"/>
/// view-projection into it before each draw.
/// </summary>
/// <remarks>
/// <para>
/// Use this in place of a bare <see cref="Matrix4x4"/> on shader
/// sets where you want the renderer to apply the camera
/// automatically. Carries an implicit conversion from
/// <see cref="Matrix4x4"/> so call sites stay clean: passing a
/// model matrix where a <c>Transform</c> is expected just works.
/// </para>
/// <para>
/// When the args struct is passed through
/// <see cref="Renderer3D.DrawSceneMesh{TVertex,TArgs}(Mesh{TVertex},
/// ShaderSet{TVertex,TArgs}, in TArgs)"/> with a non-null
/// <see cref="Renderer3D.Camera"/>, the value the GPU sees is
/// <c>Matrix * camera.GetViewProjection(aspect)</c> -- so the
/// caller passes a model matrix and the shader receives an MVP.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct TransformArgs : IUniformArgs<TransformArgs>
{
    /// <summary>The transform matrix the shader receives.</summary>
    public Matrix4x4 Matrix;

    public TransformArgs(Matrix4x4 matrix)
    {
        Matrix = matrix;
    }

    /// <summary>
    /// Implicit conversion from <see cref="Matrix4x4"/> so callers
    /// can pass a bare model matrix to scene-aware draw overloads.
    /// </summary>
    public static implicit operator TransformArgs(Matrix4x4 matrix) =>
        new TransformArgs(matrix);

    /// <inheritdoc cref="IUniformArgs{TSelf}.GetTransform"/>
    public static Func<TransformArgs, Matrix4x4>? GetTransform { get; } =
        args => args.Matrix;

    /// <inheritdoc cref="IUniformArgs{TSelf}.SetTransform"/>
    public static Func<TransformArgs, Matrix4x4, TransformArgs>? SetTransform { get; } =
        (args, m) => new TransformArgs(m);
}

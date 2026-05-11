using System.Numerics;
using System.Runtime.InteropServices;

namespace Blitter;

/// <summary>
/// Per-draw arguments combining a 4x4 transform with a floating-point
/// RGBA color (0..1 per channel). Pairs with
/// <see cref="Shaders.PositionWithTransformAndColor"/>. The "F" prefix
/// follows SDL's <c>SDL_FColor</c> convention -- it's the same idea as
/// <see cref="TransformAndColorInstance"/>, just at float precision instead of
/// 8 bits per channel.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct TransformAndFColorArgs : IUniformArgs<TransformAndFColorArgs>
{
    /// <summary>4x4 transform matrix.</summary>
    public Matrix4x4 Transform;

    /// <summary>RGBA color with each channel in the 0..1 range.</summary>
    public Vector4 FColor;

    public TransformAndFColorArgs(Matrix4x4 transform, Vector4 fColor)
    {
        Transform = transform;
        FColor = fColor;
    }

    public TransformAndFColorArgs(Matrix4x4 transform)
        : this(transform, new Vector4(1f, 1f, 1f, 1f))
    {
    }

    /// <inheritdoc cref="IUniformArgs{TSelf}.GetTransform"/>
    public static Func<TransformAndFColorArgs, Matrix4x4>? GetTransform { get; } =
        args => args.Transform;

    /// <inheritdoc cref="IUniformArgs{TSelf}.SetTransform"/>
    public static Func<TransformAndFColorArgs, Matrix4x4, TransformAndFColorArgs>? SetTransform { get; } =
        (args, m) => new TransformAndFColorArgs(m, args.FColor);
}

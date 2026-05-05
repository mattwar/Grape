using System.Numerics;
using System.Runtime.InteropServices;

namespace Grape;

/// <summary>
/// Per-draw arguments for <see cref="Shaders.PositionWithTransformAndColor"/>: a
/// 4x4 model-view-projection matrix consumed by the vertex stage at slot 0,
/// followed by an RGBA color consumed by the fragment stage at slot 0.
/// Field declaration order must match the corresponding
/// <see cref="ShaderArgsLayout"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct TransformAndColorArgs
{
    /// <summary>Model-view-projection matrix (vertex slot 0).</summary>
    public Matrix4x4 Mvp;

    /// <summary>RGBA color emitted by every fragment (fragment slot 0).</summary>
    public Vector4 Color;
}

using System.Numerics;
using System.Runtime.InteropServices;

namespace Grape;

/// <summary>
/// Per-instance data for the built-in <c>*Instanced</c> shader sets: a
/// 4x4 world transform plus an 8-bit RGBA color. The color is multiplied
/// into the mesh's vertex / sampled color in the shader, so
/// <see cref="Color.White"/> leaves the source color unchanged.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct TransformAndColorInstance
{
    /// <summary>4x4 world transform applied to the mesh for this instance.</summary>
    public Matrix4x4 Transform;

    /// <summary>RGBA color multiplied into the fragment color.</summary>
    public Color Color;

    public TransformAndColorInstance(Matrix4x4 transform, Color color)
    {
        Transform = transform;
        Color = color;
    }

    public TransformAndColorInstance(Matrix4x4 transform)
        : this(transform, Color.White)
    {
    }
}

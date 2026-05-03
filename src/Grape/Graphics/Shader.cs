using System.Numerics;

namespace Grape;

/// <summary>
/// A vertex+fragment shader pair bound to a specific vertex layout.
/// </summary>
public abstract class Shader
{
    private protected Shader(
        GpuShader vertexShader,
        GpuShader fragmentShader,
        VertexLayout vertexLayout,
        bool requiresTransform)
    {
        ArgumentNullException.ThrowIfNull(vertexShader);
        ArgumentNullException.ThrowIfNull(fragmentShader);
        ArgumentNullException.ThrowIfNull(vertexLayout);

        VertexShader = vertexShader;
        FragmentShader = fragmentShader;
        VertexLayout = vertexLayout;
        RequiresTransform = requiresTransform;
    }

    internal GpuShader VertexShader { get; }
    internal GpuShader FragmentShader { get; }
    public VertexLayout VertexLayout { get; }

    /// <summary>
    /// True if the vertex shader reads a 4x4 transformation matrix from
    /// vertex uniform slot 0. The renderer will push the per-draw transform
    /// (or <see cref="Matrix4x4.Identity"/>) before each draw call.
    /// </summary>
    public bool RequiresTransform { get; }
}

/// <summary>
/// A vertex+fragment shader pair bound to a specific vertex layout.
/// </summary>
/// <typeparam name="TVertex">
/// The vertex struct that meshes drawn with this shader must use.
/// </typeparam>
public sealed class Shader<TVertex> : Shader where TVertex : unmanaged
{
    internal Shader(
        GpuShader vertexShader,
        GpuShader fragmentShader,
        VertexLayout vertexLayout,
        bool requiresTransform = false)
        : base(vertexShader, fragmentShader, vertexLayout, requiresTransform)
    {
    }
}

using System.Collections.Immutable;
using System.Numerics;

namespace Grape;

/// <summary>
/// Resource counts a shader stage exposes to the pipeline. Must match what
/// the bytecode actually declares; the GPU validator will reject mismatches.
/// </summary>
public readonly record struct ShaderResourceCounts(
    uint NumSamplers = 0,
    uint NumUniformBuffers = 0,
    uint NumStorageTextures = 0,
    uint NumStorageBuffers = 0);

/// <summary>Pipeline stage a <see cref="StageShader"/> targets.</summary>
public enum StageShaderKind
{
    Vertex,
    Fragment,
}

/// <summary>Bytecode format of a <see cref="StageShader"/>.</summary>
public enum ShaderFormat
{
    Spirv,
    Dxil,
    Msl,
}

/// <summary>
/// Compiled bytecode for a single shader stage. Carries enough information
/// for a renderer to lazily upload it to the GPU; not itself bound to any
/// device. Two <see cref="StageShader"/> instances with the same bytes are
/// distinct objects and produce distinct GPU resources.
/// </summary>
public sealed class StageShader
{
    public StageShader(
        StageShaderKind kind,
        ShaderFormat format,
        ImmutableArray<byte> code,
        ShaderResourceCounts resources = default,
        string entrypoint = "main")
    {
        if (code.IsDefaultOrEmpty)
            throw new ArgumentException("Stage shader code cannot be empty.", nameof(code));
        ArgumentException.ThrowIfNullOrEmpty(entrypoint);

        Kind = kind;
        Format = format;
        Code = code;
        Resources = resources;
        Entrypoint = entrypoint;
    }

    public StageShaderKind Kind { get; }
    public ShaderFormat Format { get; }
    public ImmutableArray<byte> Code { get; }
    public ShaderResourceCounts Resources { get; }
    public string Entrypoint { get; }
}

/// <summary>
/// A vertex+fragment shader pair bound to a specific vertex layout. Holds
/// CPU-side bytecode; GPU resources are created lazily by a renderer when
/// the shader is first drawn.
/// </summary>
public abstract class Shader
{
    private protected Shader(
        StageShader vertex,
        StageShader fragment,
        VertexLayout vertexLayout,
        bool requiresTransform)
    {
        ArgumentNullException.ThrowIfNull(vertex);
        ArgumentNullException.ThrowIfNull(fragment);
        ArgumentNullException.ThrowIfNull(vertexLayout);
        if (vertex.Kind != StageShaderKind.Vertex)
            throw new ArgumentException("Vertex stage must have Vertex kind.", nameof(vertex));
        if (fragment.Kind != StageShaderKind.Fragment)
            throw new ArgumentException("Fragment stage must have Fragment kind.", nameof(fragment));

        Vertex = vertex;
        Fragment = fragment;
        VertexLayout = vertexLayout;
        RequiresTransform = requiresTransform;
    }

    public StageShader Vertex { get; }
    public StageShader Fragment { get; }
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
    public Shader(
        StageShader vertex,
        StageShader fragment,
        VertexLayout vertexLayout,
        bool requiresTransform = false)
        : base(vertex, fragment, vertexLayout, requiresTransform)
    {
    }
}

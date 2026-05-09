using System.Runtime.CompilerServices;

namespace Blitter.Shaders;

/// <summary>
/// A vertex+fragment shader pair bound to a specific vertex layout. Holds
/// CPU-side bytecode; GPU resources are created lazily by a renderer when
/// the shader is first drawn.
/// </summary>
public abstract class ShaderSet
{
    private protected ShaderSet(
        Shader vertex,
        Shader fragment,
        ShaderVertexLayout vertexLayout)
    {
        ArgumentNullException.ThrowIfNull(vertex);
        ArgumentNullException.ThrowIfNull(fragment);
        ArgumentNullException.ThrowIfNull(vertexLayout);
        if (vertex.Kind != ShaderKind.Vertex)
            throw new ArgumentException("Vertex stage must have Vertex kind.", nameof(vertex));
        if (fragment.Kind != ShaderKind.Fragment)
            throw new ArgumentException("Fragment stage must have Fragment kind.", nameof(fragment));

        Vertex = vertex;
        Fragment = fragment;
        VertexLayout = vertexLayout;
    }

    /// <summary>
    /// The vertex shader stage. Must have <see cref="ShaderKind.Vertex"/> kind.   
    /// </summary>
    public Shader Vertex { get; }

    /// <summary>
    /// The fragment shader stage. Must have <see cref="ShaderKind.Fragment"/> kind.
    /// </summary>
    public Shader Fragment { get; }

    /// <summary>
    /// The layout of the vertex data the vertex shader receives.
    /// </summary>
    public ShaderVertexLayout VertexLayout { get; }
}

/// <summary>
/// A vertex+fragment shader pair bound to a specific vertex layout.
/// </summary>
/// <typeparam name="TVertex">
/// The vertex struct that meshes drawn with this shader must use.
/// </typeparam>
public class ShaderSet<TVertex> : ShaderSet where TVertex : unmanaged
{
    public ShaderSet(
        Shader vertex,
        Shader fragment,
        ShaderVertexLayout vertexLayout)
        : base(vertex, fragment, vertexLayout)
    {
    }
}

/// <summary>
/// A shader pair that also accepts a typed per-draw arguments value. The
/// bytes of <typeparamref name="TArgs"/> are split across stage/slot pairs as
/// described by <see cref="ArgsLayout"/>; the renderer pushes each slot
/// before the draw.
/// </summary>
/// <typeparam name="TVertex">
/// The vertex struct that meshes drawn with this shader must use.
/// </typeparam>
/// <typeparam name="TArgs">
/// An <see cref="System.Runtime.InteropServices.StructLayoutAttribute"/>-friendly
/// unmanaged struct whose fields, in declaration order, correspond to the
/// elements of <see cref="ArgsLayout"/>. <c>sizeof(TArgs)</c> must
/// equal <see cref="ShaderArgsLayout.TotalSize"/>.
/// </typeparam>
public sealed class ShaderSet<TVertex, TArgs> : ShaderSet<TVertex>
    where TVertex : unmanaged
    where TArgs : unmanaged
{
    public ShaderSet(
        Shader vertex,
        Shader fragment,
        ShaderVertexLayout vertexLayout,
        ShaderArgsLayout argsLayout)
        : base(vertex, fragment, vertexLayout)
    {
        ArgumentNullException.ThrowIfNull(argsLayout);

        var actual = Unsafe.SizeOf<TArgs>();
        if (actual != argsLayout.TotalSize)
            throw new ArgumentException(
                $"sizeof({typeof(TArgs).Name}) = {actual} but ShaderArgsLayout describes {argsLayout.TotalSize} bytes.",
                nameof(argsLayout));

        ArgsLayout = argsLayout;
    }

    /// <summary>
    /// The layout of the per-draw arguments the shaders receive.
    /// This describes the arguments for both stages together, so the data can be supplied by the user in one struct.
    /// </summary>
    public ShaderArgsLayout ArgsLayout { get; }
}

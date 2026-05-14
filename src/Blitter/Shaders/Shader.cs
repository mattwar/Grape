using System.Runtime.CompilerServices;

namespace Blitter;

/// <summary>
/// A vertex+fragment shader pair bound to a specific vertex layout. Holds
/// CPU-side bytecode; GPU resources are created lazily by a renderer when
/// the shader is first drawn.
/// </summary>
public abstract class Shader
{
    private protected Shader(
        VertexShader vertex,
        FragmentShader fragment,
        ShaderVertexLayout vertexLayout,
        ShaderTextureLayout? textureLayout = null)
    {
        ArgumentNullException.ThrowIfNull(vertex);
        ArgumentNullException.ThrowIfNull(fragment);
        ArgumentNullException.ThrowIfNull(vertexLayout);

        Vertex = vertex;
        Fragment = fragment;
        VertexLayout = vertexLayout;
        TextureLayout = textureLayout ?? ShaderTextureLayout.Empty;
    }

    /// <summary>The vertex stage.</summary>
    public VertexShader Vertex { get; }

    /// <summary>The fragment stage.</summary>
    public FragmentShader Fragment { get; }

    /// <summary>
    /// The layout of the vertex data the vertex shader receives.
    /// </summary>
    public ShaderVertexLayout VertexLayout { get; }

    /// <summary>
    /// The texture/sampler bindings the shader's fragment stage expects.
    /// Defaults to <see cref="ShaderTextureLayout.Empty"/>; pass an
    /// explicit layout (e.g. <see cref="ShaderTextureLayout.SingleTexture2D"/>)
    /// for shaders that bind textures.
    /// </summary>
    public ShaderTextureLayout TextureLayout { get; }
}

/// <summary>
/// A vertex+fragment shader pair bound to a specific vertex layout.
/// </summary>
/// <typeparam name="TVertex">
/// The vertex struct that meshes drawn with this shader must use.
/// </typeparam>
public class Shader<TVertex> : Shader where TVertex : unmanaged
{
    public Shader(
        VertexShader vertex,
        FragmentShader fragment,
        ShaderVertexLayout vertexLayout,
        ShaderTextureLayout? textureLayout = null)
        : base(vertex, fragment, vertexLayout, textureLayout)
    {
    }

    public Shader(
        string vertex,
        string fragment,
        ShaderVertexLayout vertexLayout,
        ShaderTextureLayout? textureLayout = null)
        : this(
            new VertexShader(vertex),
            new FragmentShader(fragment),
            vertexLayout,
            textureLayout)
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
public sealed class Shader<TVertex, TArgs> : Shader<TVertex>
    where TVertex : unmanaged
    where TArgs : unmanaged
{
    public Shader(
        VertexShader vertex,
        FragmentShader fragment,
        ShaderVertexLayout vertexLayout,
        ShaderArgsLayout argsLayout,
        ShaderTextureLayout? textureLayout = null)
        : base(vertex, fragment, vertexLayout, textureLayout)
    {
        ArgumentNullException.ThrowIfNull(argsLayout);

        var actual = Unsafe.SizeOf<TArgs>();
        if (actual != argsLayout.TotalSize)
        {
            throw new ArgumentException(
                $"sizeof({typeof(TArgs).Name}) = {actual} but ShaderArgsLayout describes {argsLayout.TotalSize} bytes.",
                nameof(argsLayout));
        }

        ArgsLayout = argsLayout;
    }

    public Shader(
        string vertex,
        string fragment,
        ShaderVertexLayout vertexLayout,
        ShaderArgsLayout argsLayout,
        ShaderTextureLayout? textureLayout = null)
        : this(
            new VertexShader(vertex),
            new FragmentShader(fragment),
            vertexLayout,
            argsLayout,
            textureLayout)
    {       
    }

    /// <summary>
    /// The layout of the per-draw arguments the shaders receive.
    /// This describes the arguments for both stages together, so the data can be supplied by the user in one struct.
    /// </summary>
    public ShaderArgsLayout ArgsLayout { get; }
}

/// <summary>
/// A shader pair used for instanced drawing. Like
/// <see cref="Shader{TVertex,TArgs}"/>, but each draw also supplies a span
/// of <typeparamref name="TInstance"/> values: the GPU executes the vertex
/// shader once per (vertex, instance) pair, and the per-instance fields
/// are bound on a second vertex slot at instance step rate.
/// </summary>
/// <typeparam name="TVertex">
/// The per-vertex struct. Same role as in <see cref="Shader{TVertex}"/>.
/// </typeparam>
/// <typeparam name="TArgs">
/// The per-call uniform args struct. One copy is pushed once per draw and
/// is shared by every instance in the call. <c>sizeof(TArgs)</c> must
/// equal <see cref="ShaderArgsLayout.TotalSize"/>.
/// </typeparam>
/// <typeparam name="TInstance">
/// The per-instance struct. One value per copy is consumed from the
/// instance buffer. <c>sizeof(TInstance)</c> must equal the byte size
/// described by <see cref="InstanceLayout"/>.
/// </typeparam>
public sealed class Shader<TVertex, TArgs, TInstance> : Shader<TVertex>
    where TVertex : unmanaged
    where TArgs : unmanaged
    where TInstance : unmanaged
{
    public Shader(
        VertexShader vertex,
        FragmentShader fragment,
        ShaderVertexLayout vertexLayout,
        ShaderVertexLayout instanceLayout,
        ShaderArgsLayout argsLayout,
        ShaderTextureLayout? textureLayout = null)
        : base(vertex, fragment, vertexLayout, textureLayout)
    {
        ArgumentNullException.ThrowIfNull(instanceLayout);
        ArgumentNullException.ThrowIfNull(argsLayout);

        var argsActual = Unsafe.SizeOf<TArgs>();
        if (argsActual != argsLayout.TotalSize)
        {
            throw new ArgumentException(
                $"sizeof({typeof(TArgs).Name}) = {argsActual} but ShaderArgsLayout describes {argsLayout.TotalSize} bytes.",
                nameof(argsLayout));
        }

        var instanceActual = Unsafe.SizeOf<TInstance>();
        var instanceExpected = SizeOfVertexLayout(instanceLayout);
        if (instanceActual != instanceExpected)
        {
            throw new ArgumentException(
                $"sizeof({typeof(TInstance).Name}) = {instanceActual} but instance ShaderVertexLayout describes {instanceExpected} bytes.",
                nameof(instanceLayout));
        }

        InstanceLayout = instanceLayout;
        ArgsLayout = argsLayout;
    }

    /// <summary>
    /// Layout of the per-instance data the vertex shader receives on the
    /// instance-rate vertex slot. Each element corresponds to one field
    /// of <typeparamref name="TInstance"/> in declaration order.
    /// </summary>
    public ShaderVertexLayout InstanceLayout { get; }

    /// <summary>
    /// Layout of the per-call uniform args. Same role as
    /// <see cref="Shader{TVertex,TArgs}.ArgsLayout"/>.
    /// </summary>
    public ShaderArgsLayout ArgsLayout { get; }

    private static int SizeOfVertexLayout(ShaderVertexLayout layout)
    {
        int total = 0;
        foreach (var element in layout.Elements)
            total += SizeOfElement(element.Kind);
        return total;
    }

    private static int SizeOfElement(ShaderVertexElementKind kind) => kind switch
    {
        ShaderVertexElementKind.Position3          => 12,
        ShaderVertexElementKind.Normal3            => 12,
        ShaderVertexElementKind.TextureCoordinate2 => 8,
        ShaderVertexElementKind.Color4             => 4,
        ShaderVertexElementKind.Float4             => 16,
        ShaderVertexElementKind.Matrix4x4          => 64,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}

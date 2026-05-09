using System.Runtime.CompilerServices;
namespace Blitter.Shaders;

/// <summary>
/// A shader pair used for instanced drawing. Like
/// <see cref="ShaderSet{TVertex,TArgs}"/>, but each draw also supplies a
/// span of <typeparamref name="TInstance"/> values: the GPU executes the
/// vertex shader once per (vertex, instance) pair, and the per-instance
/// fields are bound on a second vertex slot at instance step rate.
/// </summary>
/// <typeparam name="TVertex">
/// The per-vertex struct. Same role as in <see cref="ShaderSet{TVertex}"/>.
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
public sealed class InstancedShaderSet<TVertex, TArgs, TInstance> : ShaderSet<TVertex>
    where TVertex : unmanaged
    where TArgs : unmanaged
    where TInstance : unmanaged
{
    public InstancedShaderSet(
        Shader vertex,
        Shader fragment,
        ShaderVertexLayout vertexLayout,
        ShaderVertexLayout instanceLayout,
        ShaderArgsLayout argsLayout)
        : base(vertex, fragment, vertexLayout)
    {
        ArgumentNullException.ThrowIfNull(instanceLayout);
        ArgumentNullException.ThrowIfNull(argsLayout);

        var argsActual = Unsafe.SizeOf<TArgs>();
        if (argsActual != argsLayout.TotalSize)
            throw new ArgumentException(
                $"sizeof({typeof(TArgs).Name}) = {argsActual} but ShaderArgsLayout describes {argsLayout.TotalSize} bytes.",
                nameof(argsLayout));

        var instanceActual = Unsafe.SizeOf<TInstance>();
        var instanceExpected = SizeOfVertexLayout(instanceLayout);
        if (instanceActual != instanceExpected)
            throw new ArgumentException(
                $"sizeof({typeof(TInstance).Name}) = {instanceActual} but instance ShaderVertexLayout describes {instanceExpected} bytes.",
                nameof(instanceLayout));

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
    /// <see cref="ShaderSet{TVertex,TArgs}.ArgsLayout"/>.
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

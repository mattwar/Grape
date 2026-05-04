using System.Collections.Immutable;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

    /// <summary>
    /// Loads a precompiled SPIR-V shader, deriving the stage kind, entry-point
    /// name, and resource counts from the module itself via reflection. This
    /// is the recommended way to consume <c>.spv</c> files that were produced
    /// elsewhere -- the caller does not need to know the resource counts up
    /// front, the same way loading a PNG does not require the caller to know
    /// the image's pixel format.
    /// </summary>
    /// <param name="code">The raw SPIR-V module bytes.</param>
    /// <returns>
    /// A <see cref="StageShader"/> whose <see cref="Format"/> is
    /// <see cref="ShaderFormat.Spirv"/>, with metadata populated from the
    /// module.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The bytes are not a valid SPIR-V module, or describe more than one
    /// entry point, or use an unsupported execution model.
    /// </exception>
    public static StageShader LoadSpirv(ReadOnlySpan<byte> code)
    {
        var info = SpirvReflection.GetShaderInfo(code);
        return new StageShader(
            info.Stage,
            ShaderFormat.Spirv,
            ImmutableCollectionsMarshal.AsImmutableArray(code.ToArray()),
            info.Resources,
            info.Entrypoint);
    }

    /// <summary>
    /// Convenience overload of <see cref="LoadSpirv(ReadOnlySpan{byte})"/>
    /// that reads the SPIR-V bytes from <paramref name="path"/>.
    /// </summary>
    public static StageShader LoadSpirv(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return LoadSpirv(File.ReadAllBytes(path));
    }

    /// <summary>
    /// Writes this shader's bytes to <paramref name="path"/>. The file's
    /// contents are exactly <see cref="Code"/>, so for SPIR-V shaders the
    /// resulting file is a standard <c>.spv</c> module that any conforming
    /// loader (including <see cref="LoadSpirv(string)"/>) can read back.
    /// </summary>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        File.WriteAllBytes(path, Code.AsSpan().ToArray());
    }
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
        ShaderVertexLayout vertexLayout)
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
    }

    /// <summary>
    /// The vertex shader stage. Must have <see cref="StageShaderKind.Vertex"/> kind.   
    /// </summary>
    public StageShader Vertex { get; }

    /// <summary>
    /// The fragment shader stage. Must have <see cref="StageShaderKind.Fragment"/> kind.
    /// </summary>
    public StageShader Fragment { get; }

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
public class Shader<TVertex> : Shader where TVertex : unmanaged
{
    public Shader(
        StageShader vertex,
        StageShader fragment,
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
public sealed class Shader<TVertex, TArgs> : Shader<TVertex>
    where TVertex : unmanaged
    where TArgs : unmanaged
{
    public Shader(
        StageShader vertex,
        StageShader fragment,
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

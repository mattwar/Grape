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

    /// <summary>
    /// Compiles HLSL source code at runtime into a shader for the format the
    /// current GPU device expects (SPIR-V on Vulkan, DXIL on D3D12, MSL on
    /// Metal). Resource counts and entry-point metadata are reflected from
    /// the compiled SPIR-V automatically -- the caller doesn't need to know
    /// how many uniform buffers or samplers the shader uses.
    /// </summary>
    /// <param name="hlslSource">The HLSL source code to compile.</param>
    /// <param name="kind">The pipeline stage to compile for.</param>
    /// <param name="entrypoint">
    /// The entry-point function name in <paramref name="hlslSource"/>.
    /// Defaults to <c>"main"</c>.
    /// </param>
    /// <remarks>
    /// Compilation is delegated to <c>SDL_shadercross</c>, which bundles
    /// <c>dxcompiler</c> and SPIRV-Cross. The first call may have noticeable
    /// startup cost (tens of ms) as those native components initialize;
    /// subsequent calls are fast.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// HLSL compilation failed; <see cref="SDL3.SDL.GetError"/> contains the
    /// underlying message.
    /// </exception>
    public static StageShader Compile(
        string hlslSource,
        StageShaderKind kind,
        string entrypoint = "main") =>
        Compile(hlslSource, kind, ResolveDeviceTargetFormat(), entrypoint);

    /// <summary>
    /// Compiles HLSL source code at runtime into a shader of the requested
    /// <paramref name="targetFormat"/>. Use this overload when you need to
    /// produce bytecode for a specific format regardless of which backend
    /// the current GPU device chose -- e.g. when pre-baking shader bundles.
    /// </summary>
    /// <inheritdoc cref="Compile(string, StageShaderKind, string)"
    ///     path="/exception"/>
    public static StageShader Compile(
        string hlslSource,
        StageShaderKind kind,
        ShaderFormat targetFormat,
        string entrypoint = "main")
    {
        ArgumentException.ThrowIfNullOrEmpty(hlslSource);
        ArgumentException.ThrowIfNullOrEmpty(entrypoint);

        EnsureShaderCrossInitialized();

        // Always go HLSL -> SPIR-V first: shadercross uses DXC as the HLSL
        // front end and produces SPIR-V natively, then SPIRV-Cross translates
        // onward to MSL, etc. The SPIR-V copy doubles as our reflection
        // source so we always learn the resource counts the same way,
        // regardless of which target format we emit.
        using var hlslInfo = new SDL3.ShaderCross.HLSLInfo
        {
            Source = hlslSource,
            Entrypoint = entrypoint,
            ShaderStage = kind switch
            {
                StageShaderKind.Vertex   => SDL3.ShaderCross.ShaderStage.Vertex,
                StageShaderKind.Fragment => SDL3.ShaderCross.ShaderStage.Fragment,
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            },
        };

        var spirvPtr = SDL3.ShaderCross.CompileSPIRVFromHLSL(in hlslInfo, out var spirvSize);
        if (spirvPtr == IntPtr.Zero)
            throw new InvalidOperationException(
                $"HLSL -> SPIR-V compile failed: {SDL3.SDL.GetError()}");

        SpirvShaderInfo info;
        byte[] spirvBytes;
        try
        {
            spirvBytes = GC.AllocateUninitializedArray<byte>((int)spirvSize);
            Marshal.Copy(spirvPtr, spirvBytes, 0, spirvBytes.Length);
            info = SpirvReflection.GetShaderInfo(spirvBytes);
        }
        finally
        {
            // We're about to either reuse spirvBytes (managed) or transpile
            // to a different format from a fresh SPIRVInfo, so the native
            // SPIR-V buffer is no longer needed.
            SDL3.SDL.Free(spirvPtr);
        }

        if (info.Stage != kind)
            throw new InvalidOperationException(
                $"Compiled HLSL has stage {info.Stage} but caller requested {kind}.");

        byte[] resultBytes;
        switch (targetFormat)
        {
            case ShaderFormat.Spirv:
                resultBytes = spirvBytes;
                break;

            case ShaderFormat.Dxil:
            {
                // Compile straight from HLSL to DXIL via DXC -- avoids a
                // SPIR-V round trip and gives us the highest-fidelity DXIL.
                var dxilPtr = SDL3.ShaderCross.CompileDXILFromHLSL(in hlslInfo, out var dxilSize);
                if (dxilPtr == IntPtr.Zero)
                    throw new InvalidOperationException(
                        $"HLSL -> DXIL compile failed: {SDL3.SDL.GetError()}");
                try
                {
                    resultBytes = GC.AllocateUninitializedArray<byte>((int)dxilSize);
                    Marshal.Copy(dxilPtr, resultBytes, 0, resultBytes.Length);
                }
                finally
                {
                    SDL3.SDL.Free(dxilPtr);
                }
                break;
            }

            case ShaderFormat.Msl:
            {
                resultBytes = TranspileToMsl(spirvBytes, info.Stage, info.Entrypoint);
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(targetFormat), targetFormat, "Unknown shader format.");
        }

        return new StageShader(
            kind,
            targetFormat,
            ImmutableCollectionsMarshal.AsImmutableArray(resultBytes),
            info.Resources,
            info.Entrypoint);
    }

    /// <summary>
    /// Picks a single concrete shader format from the GPU device's reported
    /// supported set, using the same priority order as
    /// <c>BuiltInShaders</c>: SPIR-V first, then DXIL, then MSL.
    /// </summary>
    private static ShaderFormat ResolveDeviceTargetFormat()
    {
        var supported = GpuDevice.Default.ShaderFormat;
        if ((supported & SDL3.SDL.GPUShaderFormat.SPIRV) != 0) return ShaderFormat.Spirv;
        if ((supported & SDL3.SDL.GPUShaderFormat.DXIL) != 0)  return ShaderFormat.Dxil;
        if ((supported & SDL3.SDL.GPUShaderFormat.MSL) != 0)   return ShaderFormat.Msl;
        throw new NotSupportedException(
            $"GPU device reports no shader format supported by Grape. Reported: {supported}.");
    }

    /// <summary>
    /// Transpiles SPIR-V bytecode to MSL source via SPIRV-Cross. SDL3's
    /// Metal backend consumes MSL as a NUL-terminated UTF-8 string, so the
    /// returned bytes include a trailing zero byte.
    /// </summary>
    private static unsafe byte[] TranspileToMsl(
        ReadOnlySpan<byte> spirvBytes,
        StageShaderKind stage,
        string entrypoint)
    {
        fixed (byte* pSpirv = spirvBytes)
        {
            using var spv = new SDL3.ShaderCross.SPIRVInfo
            {
                ByteCode = (IntPtr)pSpirv,
                ByteCodeSize = (UIntPtr)spirvBytes.Length,
                Entrypoint = entrypoint,
                ShaderStage = stage switch
                {
                    StageShaderKind.Vertex   => SDL3.ShaderCross.ShaderStage.Vertex,
                    StageShaderKind.Fragment => SDL3.ShaderCross.ShaderStage.Fragment,
                    _ => throw new ArgumentOutOfRangeException(nameof(stage)),
                },
            };

            var mslPtr = SDL3.ShaderCross.TranspileMSLFromSPIRV(in spv);
            if (mslPtr == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"SPIR-V -> MSL transpile failed: {SDL3.SDL.GetError()}");
            try
            {
                var text = Marshal.PtrToStringUTF8(mslPtr) ?? string.Empty;
                var byteCount = System.Text.Encoding.UTF8.GetByteCount(text);
                var bytes = new byte[byteCount + 1]; // +1 for NUL terminator
                System.Text.Encoding.UTF8.GetBytes(text, bytes);
                bytes[byteCount] = 0;
                return bytes;
            }
            finally
            {
                SDL3.SDL.Free(mslPtr);
            }
        }
    }

    private static int _shaderCrossInitialized;

    /// <summary>
    /// Lazily initializes <c>SDL_shadercross</c> on first use. The native
    /// library is reference-counted internally and safe to call from any
    /// thread, but we still gate on a flag to keep startup cheap.
    /// </summary>
    private static void EnsureShaderCrossInitialized()
    {
        if (Interlocked.CompareExchange(ref _shaderCrossInitialized, 1, 0) != 0)
            return;
        if (!SDL3.ShaderCross.Init())
        {
            // Reset so a later attempt can retry once the underlying issue
            // (e.g., missing native dependency) is fixed.
            Interlocked.Exchange(ref _shaderCrossInitialized, 0);
            throw new InvalidOperationException(
                $"Failed to initialize SDL_shadercross: {SDL3.SDL.GetError()}");
        }
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

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Blitter;

/// <summary>
/// One stage of a shader pipeline. Concrete subclasses are
/// <see cref="VertexShader"/> and <see cref="FragmentShader"/>.
/// </summary>
public abstract class StageShader
{
    // The HLSL source text, if supplied at construction.
    private readonly string? _source;

    // Cache of compiled bytecode by format. Populated lazily on demand
    private readonly Dictionary<ShaderFormat, ImmutableArray<byte>> _byteCache = new();

    // Format of the bytes the user gave us at construction, if any. 
    private readonly ShaderFormat? _sourceByteFormat;

    // Cached resource counts, reflected from SPIR-V on demand if not supplied
    private ShaderResourceCounts? _resources;

    private readonly object _lock = new();

    /// <summary>
    /// Constructs a stage shader from HLSL source text. Compilation is
    /// deferred until <see cref="GetCode(ShaderFormat)"/> or
    /// <see cref="GetResources"/> is called, so creating a shader is
    /// essentially free.
    /// </summary>
    /// <param name="kind">The pipeline stage this shader targets.</param>
    /// <param name="source">The HLSL source code.</param>
    /// <param name="entrypoint">
    /// The entry-point function name in <paramref name="source"/>.
    /// Defaults to <c>"main"</c>.
    /// </param>
    private protected StageShader(ShaderKind kind, string source, string entrypoint = "main")
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        ArgumentException.ThrowIfNullOrEmpty(entrypoint);

        Kind = kind;
        _source = source;
        Entrypoint = entrypoint;
    }

    /// <summary>
    /// Constructs a shader from precompiled bytes in a known format.
    /// </summary>
    /// <param name="kind">The pipeline stage this shader targets.</param>
    /// <param name="format">The format of <paramref name="code"/>.</param>
    /// <param name="code">The precompiled shader bytes.</param>
    /// <param name="entrypoint">The entry-point function name.</param>
    /// <param name="resources">
    /// The shader's resource counts. Required when <paramref name="format"/>
    /// is <see cref="ShaderFormat.Dxil"/> or <see cref="ShaderFormat.Msl"/>
    /// because Blitter cannot reflect those formats; optional for
    /// <see cref="ShaderFormat.Spirv"/> (reflected on demand if not supplied).
    /// </param>
    private protected StageShader(
        ShaderKind kind,
        ShaderFormat format,
        ImmutableArray<byte> code,
        string entrypoint = "main",
        ShaderResourceCounts? resources = null)
    {
        if (code.IsDefaultOrEmpty)
            throw new ArgumentException("Stage shader code cannot be empty.", nameof(code));
        ArgumentException.ThrowIfNullOrEmpty(entrypoint);

        if (resources is null && format != ShaderFormat.Spirv)
            throw new ArgumentException(
                $"Resource counts must be supplied when constructing a StageShader from {format} bytes; only SPIR-V can be reflected automatically.",
                nameof(resources));

        Kind = kind;
        Entrypoint = entrypoint;
        _sourceByteFormat = format;
        _byteCache[format] = code;
        _resources = resources;
    }

    /// <summary>The pipeline stage this shader targets.</summary>
    internal ShaderKind Kind { get; }

    /// <summary>The entry-point function name within the shader source/bytecode.</summary>
    public string Entrypoint { get; }

    /// <summary>
    /// Returns the compiled shader byte code.
    /// </summary>
    public ImmutableArray<byte> GetCode(ShaderFormat format)
    {
        // Lock-free fast path on cache hit. Dictionary reads are not
        // thread-safe in the general case but a value already in the
        // dictionary is a stable reference; the worst case under contention
        // is a missed hit and a redundant compilation, which the lock below
        // prevents.
        lock (_lock)
        {
            if (_byteCache.TryGetValue(format, out var cached))
                return cached;

            var bytes = ProduceFormat(format);
            _byteCache[format] = bytes;
            return bytes;
        }
    }

    /// <summary>
    /// Returns the shader's resource counts, reflecting the SPIR-V
    /// representation on first call if not supplied at construction.
    /// </summary>
    public ShaderResourceCounts GetResources()
    {
        lock (_lock)
        {
            if (_resources is { } r) return r;

            var spirv = EnsureSpirvLocked();
            var info = SpirvReflection.GetShaderInfo(spirv.AsSpan());
            if (info.Stage != Kind)
                throw new InvalidOperationException(
                    $"StageShader source declares stage {info.Stage} but StageShader was constructed as {Kind}.");

            _resources = info.Resources;
            return info.Resources;
        }
    }

    /// <summary>
    /// Writes the shader's bytes in <paramref name="format"/> to
    /// <paramref name="path"/>. Triggers compilation/transpilation if those
    /// bytes haven't been produced yet.
    /// </summary>
    public void Save(string path, ShaderFormat format)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var bytes = GetCode(format);
        File.WriteAllBytes(path, bytes.AsSpan().ToArray());
    }

    // ---- Production / conversion ----

    /// <summary>
    /// Produces the requested format from whatever sources we have. Caller
    /// holds <see cref="_lock"/>.
    /// </summary>
    private ImmutableArray<byte> ProduceFormat(ShaderFormat format)
    {
        // If we have HLSL source, we can produce anything.
        if (_source is not null)
            return CompileHlslTo(format);

        // Otherwise we have bytes in _sourceByteFormat. Conversion rules:
        // - SPIR-V -> any: supported via shadercross.
        // - DXIL or MSL -> different format: not supported.
        if (_sourceByteFormat is ShaderFormat.Spirv)
        {
            var spirv = _byteCache[ShaderFormat.Spirv];
            return format switch
            {
                ShaderFormat.Spirv => spirv,
                ShaderFormat.Dxil  => SpirvToDxil(spirv.AsSpan()),
                ShaderFormat.Msl   => SpirvToMsl(spirv.AsSpan(), Kind, Entrypoint),
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
            };
        }

        throw new InvalidOperationException(
            $"This StageShader was constructed from precompiled {_sourceByteFormat} bytes, which cannot be converted to {format}. Reconstruct from HLSL source or SPIR-V bytes if you need other formats.");
    }

    /// <summary>
    /// Ensures the SPIR-V cache slot is populated and returns it. Caller
    /// holds <see cref="_lock"/>.
    /// </summary>
    private ImmutableArray<byte> EnsureSpirvLocked()
    {
        if (_byteCache.TryGetValue(ShaderFormat.Spirv, out var spirv))
            return spirv;

        if (_source is null)
        {
            // The only byte source we accept that isn't already SPIR-V is...
            // nothing -- DXIL and MSL constructors require resources up front
            // so we never reflect them. So this branch is only reachable for
            // a SPIR-V byte source, which would already be cached above.
            throw new InvalidOperationException(
                "Cannot derive SPIR-V from this StageShader's source.");
        }

        var bytes = CompileHlslTo(ShaderFormat.Spirv);
        _byteCache[ShaderFormat.Spirv] = bytes;
        return bytes;
    }

    private ImmutableArray<byte> CompileHlslTo(ShaderFormat format)
    {
        EnsureShaderCrossInitialized();
        using var hlslInfo = new SDL3.ShaderCross.HLSLInfo
        {
            Source = _source!,
            Entrypoint = Entrypoint,
            ShaderStage = ToShaderCrossStage(Kind),
        };

        switch (format)
        {
            case ShaderFormat.Spirv:
            {
                var ptr = SDL3.ShaderCross.CompileSPIRVFromHLSL(in hlslInfo, out var size);
                return CopyAndFreeNative(ptr, size, "HLSL -> SPIR-V");
            }

            // DXIL goes straight from HLSL via DXC -- avoids a SPIR-V round
            // trip and gives us the highest-fidelity DXIL.
            case ShaderFormat.Dxil:
            {
                var ptr = SDL3.ShaderCross.CompileDXILFromHLSL(in hlslInfo, out var size);
                return CopyAndFreeNative(ptr, size, "HLSL -> DXIL");
            }

            // MSL goes via SPIR-V (the only path shadercross offers). This
            // also primes the SPIR-V cache as a side effect, which is
            // useful since MSL emission needs reflection-stage info anyway.
            case ShaderFormat.Msl:
                return SpirvToMsl(EnsureSpirvLocked().AsSpan(), Kind, Entrypoint);

            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    private static ImmutableArray<byte> SpirvToDxil(ReadOnlySpan<byte> spirv)
    {
        EnsureShaderCrossInitialized();
        unsafe
        {
            fixed (byte* p = spirv)
            {
                using var info = new SDL3.ShaderCross.SPIRVInfo
                {
                    ByteCode = (IntPtr)p,
                    ByteCodeSize = (UIntPtr)spirv.Length,
                    Entrypoint = "main",
                    // SPIRV-Cross only needs ShaderStage to validate, but
                    // shadercross requires us to set it; the actual stage
                    // is encoded in the SPIR-V's OpEntryPoint. Pass Vertex
                    // as a placeholder -- DXIL emission doesn't branch on it.
                    ShaderStage = SDL3.ShaderCross.ShaderStage.Vertex,
                };
                var ptr = SDL3.ShaderCross.CompileDXILFromSPIRV(in info, out var size);
                return CopyAndFreeNative(ptr, size, "SPIR-V -> DXIL");
            }
        }
    }

    private static ImmutableArray<byte> SpirvToMsl(
        ReadOnlySpan<byte> spirv,
        ShaderKind stage,
        string entrypoint)
    {
        EnsureShaderCrossInitialized();
        unsafe
        {
            fixed (byte* p = spirv)
            {
                using var info = new SDL3.ShaderCross.SPIRVInfo
                {
                    ByteCode = (IntPtr)p,
                    ByteCodeSize = (UIntPtr)spirv.Length,
                    Entrypoint = entrypoint,
                    ShaderStage = ToShaderCrossStage(stage),
                };
                var ptr = SDL3.ShaderCross.TranspileMSLFromSPIRV(in info);
                if (ptr == IntPtr.Zero)
                    throw new InvalidOperationException(
                        $"SPIR-V -> MSL transpile failed: {SDL3.SDL.GetError()}");
                try
                {
                    // SDL3 GPU's Metal backend takes the MSL source as a
                    // NUL-terminated UTF-8 string; preserve the terminator.
                    var text = Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
                    var byteCount = System.Text.Encoding.UTF8.GetByteCount(text);
                    var bytes = new byte[byteCount + 1];
                    System.Text.Encoding.UTF8.GetBytes(text, bytes);
                    bytes[byteCount] = 0;
                    return ImmutableCollectionsMarshal.AsImmutableArray(bytes);
                }
                finally
                {
                    SDL3.SDL.Free(ptr);
                }
            }
        }
    }

    private static SDL3.ShaderCross.ShaderStage ToShaderCrossStage(ShaderKind kind) => kind switch
    {
        ShaderKind.Vertex   => SDL3.ShaderCross.ShaderStage.Vertex,
        ShaderKind.Fragment => SDL3.ShaderCross.ShaderStage.Fragment,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static ImmutableArray<byte> CopyAndFreeNative(IntPtr ptr, UIntPtr size, string operation)
    {
        if (ptr == IntPtr.Zero)
            throw new InvalidOperationException(
                $"{operation} compile failed: {SDL3.SDL.GetError()}");
        try
        {
            var bytes = GC.AllocateUninitializedArray<byte>((int)size);
            Marshal.Copy(ptr, bytes, 0, bytes.Length);
            return ImmutableCollectionsMarshal.AsImmutableArray(bytes);
        }
        finally
        {
            SDL3.SDL.Free(ptr);
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


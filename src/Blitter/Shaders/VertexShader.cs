using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Blitter;

/// <summary>
/// A vertex-stage <see cref="StageShader"/>.
/// </summary>
public sealed class VertexShader : StageShader
{
    /// <summary>
    /// Constructs a vertex shader from HLSL source text.
    /// </summary>
    public VertexShader(string source, string entrypoint = "main")
        : base(ShaderKind.Vertex, source, entrypoint)
    {
    }

    /// <summary>
    /// Constructs a vertex shader from precompiled bytes in a known format.
    /// </summary>
    public VertexShader(
        ShaderFormat format,
        ImmutableArray<byte> code,
        string entrypoint = "main",
        ShaderResourceCounts? resources = null)
        : base(ShaderKind.Vertex, format, code, entrypoint, resources)
    {
    }

    /// <summary>
    /// Loads a vertex shader from a file containing precompiled shader code
    /// in <paramref name="format"/>.
    /// </summary>
    public static VertexShader Load(
        string path,
        ShaderFormat format,
        string entrypoint = "main",
        ShaderResourceCounts? resources = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var bytes = File.ReadAllBytes(path);
        return new VertexShader(
            format,
            ImmutableCollectionsMarshal.AsImmutableArray(bytes),
            entrypoint,
            resources);
    }
}

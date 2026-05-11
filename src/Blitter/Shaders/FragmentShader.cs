using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Blitter;

/// <summary>
/// A fragment-stage <see cref="StageShader"/>.
/// </summary>
public sealed class FragmentShader : StageShader
{
    /// <summary>
    /// Constructs a fragment shader from HLSL source text.
    /// </summary>
    public FragmentShader(string source, string entrypoint = "main")
        : base(ShaderKind.Fragment, source, entrypoint)
    {
    }

    /// <summary>
    /// Constructs a fragment shader from precompiled bytes in a known format.
    /// </summary>
    public FragmentShader(
        ShaderFormat format,
        ImmutableArray<byte> code,
        string entrypoint = "main",
        ShaderResourceCounts? resources = null)
        : base(ShaderKind.Fragment, format, code, entrypoint, resources)
    {
    }

    /// <summary>
    /// Loads a fragment shader from a file containing precompiled shader
    /// code in <paramref name="format"/>.
    /// </summary>
    public static FragmentShader Load(
        string path,
        ShaderFormat format,
        string entrypoint = "main",
        ShaderResourceCounts? resources = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var bytes = File.ReadAllBytes(path);
        return new FragmentShader(
            format,
            ImmutableCollectionsMarshal.AsImmutableArray(bytes),
            entrypoint,
            resources);
    }
}

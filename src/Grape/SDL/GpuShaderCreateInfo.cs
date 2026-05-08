using System.Collections.Immutable;
using static SDL3.SDL;

namespace Grape;

/// <summary>
/// Describes how to create a GPU shader module.
/// </summary>
internal record GpuShaderCreateInfo
{
    // GPUShaderCreateInfo

    /// <summary>
    /// The code bytes of the shader.
    /// </summary>
    public ImmutableArray<byte> Code { get; init; }

    /// <summary>
    /// A string specifying the entry point function name for the shader.
    /// </summary>
    public string Entrypoint { get; init; } = "";

    /// <summary>
    /// The format of the shader code.
    /// </summary>
    public GPUShaderFormat Format { get; init; }

    /// <summary>
    /// The stage the shader program corresponds to.
    /// </summary>
    public GPUShaderStage Stage { get; init; }

    /// <summary>
    /// The number of samplers defined in the shader.
    /// </summary>
    public UInt32 NumSamplers { get; init; }

    /// <summary>
    /// The number of storage textures defined in the shader.
    /// </summary>
    public UInt32 NumStorageTextures { get; init; }

    /// <summary>
    /// The number of storage buffers defined in the shader.
    /// </summary>
    public UInt32 NumStorageBuffers { get; init; }

    /// <summary>
    /// The number of uniform buffers defined in the shader.
    /// </summary>
    public UInt32 NumUniformBuffers { get; init; }

    /// <summary>
    /// A properties ID for extensions. Should be 0 if no extensions are needed.
    /// </summary>
    public Properties? Properties { get; init; }
}

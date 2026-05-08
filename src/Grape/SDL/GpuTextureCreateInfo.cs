using static SDL3.SDL;

namespace Grape;

internal record GpuTextureCreateInfo
{
    // GPUTextureCreateInfo;

    /// <summary>
    /// The base dimensionality of the texture.
    /// </summary>
    public GPUTextureType Type { get; init; }

    /// <summary>
    /// The pixel format of the texture.
    /// </summary>
    public GPUTextureFormat Format { get; init; }

    /// <summary>
    /// How the texture is intended to be used by the client.
    /// </summary>
    public GPUTextureUsageFlags Usage { get; init; }

    /// <summary>
    /// The width of the texture.
    /// </summary>
    public UInt32 Width { get; init; }

    /// <summary>
    /// The height of the texture.
    /// </summary>
    public UInt32 Height { get; init; }

    /// <summary>
    /// The layer count or depth of the texture. This value is treated as a layer count on 2D array textures, and as a depth value on 3D textures.
    /// </summary>
    public UInt32 LayerCountOrDepth { get; init; }

    /// <summary>
    /// The number of mip levels in the texture.
    /// </summary>
    public UInt32 NumLevels { get; init; }

    /// <summary>
    /// The number of samples per texel. Only applies if the texture is used as a render target.
    /// </summary>
    public GPUSampleCount SampleCount { get; init; }

    /// <summary>
    /// A properties ID for extensions. Should be 0 if no extensions are needed.
    /// </summary>
    public Properties? Properties { get; init; }
}

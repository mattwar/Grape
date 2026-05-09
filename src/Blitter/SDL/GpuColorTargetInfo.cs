using static SDL3.SDL;

namespace Blitter;

/// <summary>
/// Describes one color target used by a render pass.
/// </summary>
internal record GpuColorTargetInfo
{
    // GPUColorTargetInfo

    /// <summary>
    /// The texture that will be used as a color target by a render pass.
    /// </summary>
    public GpuTexture? Texture { get; init; } = default!;

    /// <summary>
    /// The mip level to use as a color target.
    /// </summary>
    public UInt32 MipLevel { get; init; }

    /// <summary>
    /// The layer index or depth plane to use as a color target. This value is treated as a layer index on 2D array and cube textures, and as a depth plane on 3D textures.
    /// </summary>
    public UInt32 LayerOrDepthPlane { get; init; }

    /// <summary>
    /// The color to clear the color target to at the start of the render pass. Ignored if public GPU_LOADOP_CLEAR is not used.
    /// </summary>
    public FColor ClearColor { get; init; }

    /// <summary>
    /// What is done with the contents of the color target at the beginning of the render pass.
    /// </summary>
    public GPULoadOp LoadOp { get; init; } 

    /// <summary>
    /// What is done with the results of the render pass.
    /// </summary>
    public GPUStoreOp StoreOp { get; init; }

    /// <summary>
    /// The texture that will receive the results of a multisample resolve operation. Ignored if a RESOLVE* store_op is not used.
    /// </summary>
    public GpuTexture? ResolveTexture { get; init; } = default!;

    /// <summary>
    /// The mip level of the resolve texture to use for the resolve operation. Ignored if a RESOLVE* store_op is not used.
    /// </summary>
    public UInt32 ResolveMipLevel { get; init; }

    /// <summary>
    /// The layer index of the resolve texture to use for the resolve operation. Ignored if a RESOLVE* store_op is not used.
    /// </summary>
    public UInt32 ResolveLayer { get; init; }

    /// <summary>
    /// true cycles the texture if the texture is bound and load_op is not LOAD
    /// </summary>
    public bool Cycle { get; init; } 

    /// <summary>
    /// true cycles the resolve texture if the resolve texture is bound. Ignored if a RESOLVE* store_op is not used.
    /// </summary>
    public bool CycleResolveTexture { get; init; }
}

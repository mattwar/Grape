using static SDL3.SDL;

namespace Blitter;

/// <summary>
/// Describes the depth and stencil target used by a render pass.
/// </summary>
internal record GpuDepthStencilTargetInfo
{
    // GPUDepthStencilTargetInfo

    /// <summary>
    /// The texture that will be used as the depth stencil target by the render pass.
    /// </summary>
    public GpuTexture? Texture { get; init; }

    /// <summary>
    /// The value to clear the depth component to at the beginning of the render pass. Ignored if public GPU_LOADOP_CLEAR is not used.
    /// </summary>
    public float ClearDepth { get; init; }

    /// <summary>
    /// What is done with the depth contents at the beginning of the render pass.
    /// </summary>
    public GPULoadOp LoadOp { get; init; }

    /// <summary>
    /// What is done with the depth results of the render pass.
    /// </summary>
    public GPUStoreOp StoreOp { get; init; }

    /// <summary>
    /// What is done with the stencil contents at the beginning of the render pass.
    /// </summary>
    public GPULoadOp StencilLoadOp { get; init; }

    /// <summary>
    /// What is done with the stencil results of the render pass.
    /// </summary>
    public GPUStoreOp StencilStoreOp { get; init; }

    /// <summary>
    /// true cycles the texture if the texture is bound and any load ops are not LOAD 
    /// </summary>
    public bool Cycle { get; init; }

    /// <summary>
    /// The value to clear the stencil component to at the beginning of the render pass. Ignored if public GPU_LOADOP_CLEAR is not used.
    /// </summary>
    public Byte ClearStencil { get; init; }
}

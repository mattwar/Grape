using System.Collections.Immutable;
using static SDL3.SDL;

namespace Grape;

/// <summary>
/// Describes the target formats and blending state for a pipeline.
/// </summary>
internal record GpuPipelineTargetInfo
{
    // GPUGraphicsPipelineTargetInfo

    /// <summary>
    /// A pointer to an array of color target descriptions.
    /// </summary>
    public ImmutableArray<SDL.GPUColorTargetDescription> ColorTargetDescriptions { get; init; }

    /// <summary>
    /// The pixel format of the depth-stencil target. Ignored if has_depth_stencil_target is false.
    /// </summary>
    public GPUTextureFormat DepthStencilFormat { get; init; }

    /// <summary>
    /// true specifies that the pipeline uses a depth-stencil target.
    /// </summary>
    public bool HasDepthStencilTarget { get; init; }
}

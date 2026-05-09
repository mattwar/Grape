using System.Collections.Immutable;
using static SDL3.SDL;

namespace Blitter;

/// <summary>
/// Describes how to create a graphics pipeline.
/// </summary>
internal record GpuPipelineCreateInfo
{
    // GPUGraphicsPipelineCreateInfo

    /// <summary>
    /// The vertex shader used by the graphics pipeline.
    /// </summary>
    public GpuShader? VertexShader { get; set; }

    /// <summary>
    /// The fragment shader used by the graphics pipeline.
    /// </summary>
    public GpuShader? FragmentShader { get; set; }

    /// <summary>
    /// The vertex layout of the graphics pipeline.
    /// </summary>
    public GpuVertexInputState VertexInputState { get; set; } = default!;

    /// <summary>
    /// The primitive topology of the graphics pipeline.
    /// </summary>
    public GPUPrimitiveType PrimitiveType { get; set; }

    /// <summary>
    /// The rasterizer state of the graphics pipeline.
    /// </summary>
    public GPURasterizerState RasterizerState { get; set; }

    /// <summary>
    /// The multisample state of the graphics pipeline.
    /// </summary>
    public GPUMultisampleState MultisampleState { get; set; }

    /// <summary>
    /// The depth-stencil state of the graphics pipeline.
    /// </summary>
    public GPUDepthStencilState DepthStencilState { get; set; }

    /// <summary>
    /// Formats and blend modes for the render targets of the graphics pipeline.
    /// </summary>
    public GpuPipelineTargetInfo TargetInfo { get; set; } = new GpuPipelineTargetInfo
    {
        ColorTargetDescriptions = ImmutableArray<SDL.GPUColorTargetDescription>.Empty,
    };

    /// <summary>
    /// Properties for extensions. Should be null if no extensions are needed.
    /// </summary>
    public Properties? Properties { get; set; }
}

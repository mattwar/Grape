using System.Collections.Immutable;

namespace Grape;

/// <summary>
/// Describes the vertex buffers and attributes used by a pipeline.
/// </summary>
internal record GpuVertexInputState
{
    // SDL.GPUVertexInputState

    public ImmutableArray<SDL.GPUVertexBufferDescription> BufferDescriptions { get; init; }
    public ImmutableArray<SDL.GPUVertexAttribute> Attributes { get; init; }
}

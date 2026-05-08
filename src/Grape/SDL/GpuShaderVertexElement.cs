using static SDL3.SDL;

namespace Grape;

/// <summary>
/// Describes one element within a vertex layout.
/// </summary>
internal record GpuShaderVertexElement
{
    /// <summary>
    /// The size and type of the attribute data.
    /// </summary>
    public GPUVertexElementFormat Format { get; init; }

    /// <summary>
    /// The byte offset of this attribute relative to the start of the vertex element.
    /// </summary>
    public UInt32 Offset { get; init; }
}

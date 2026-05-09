using System.Collections.Immutable;
using static SDL3.SDL;

namespace Blitter;

/// <summary>
/// Describes how vertex data is laid out inside a vertex buffer.
/// </summary>
internal record GpuVertexBufferLayout
{
    /// <summary>
    /// The size of a single vertex + the offset between vertices.
    /// </summary>
    public int Pitch { get; init; }

    /// <summary>
    /// The description of the data elements of each vertex.
    /// </summary>
    public ImmutableArray<GpuShaderVertexElement> Elements { get; init; }

    /// <summary>
    /// Whether attribute addressing is a function of the vertex index or instance index.
    /// </summary>
    public GPUVertexInputRate InputRate { get; init; } = GPUVertexInputRate.Vertex;
}

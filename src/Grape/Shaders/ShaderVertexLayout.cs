using System.Collections.Immutable;

namespace Grape.Shaders;

/// <summary>
/// Describes the layout of vertices in a mesh.
/// </summary>
public sealed record ShaderVertexLayout(ImmutableArray<ShaderVertexElement> Elements)
{
    public ShaderVertexLayout(params ShaderVertexElementKind[] kinds)
        : this(kinds.Select(k => new ShaderVertexElement(k)).ToImmutableArray())
    {
    }
}

/// <summary>
/// Describes one element in a vertex layout.
/// </summary>
public sealed record ShaderVertexElement(ShaderVertexElementKind Kind);

/// <summary>
/// The kind of data represented by a vertex element.
/// </summary>
public enum ShaderVertexElementKind
{
    Position3,
    /// <summary>
    /// Three-component world-space normal vector (12 bytes). Same byte layout
    /// as <see cref="Position3"/>; named separately so vertex types and
    /// shaders can document intent and so future per-element validation can
    /// distinguish them.
    /// </summary>
    Normal3,
    TextureCoordinate2,
    Color4,
    /// <summary>
    /// Four-component float vector (16 bytes). Useful as a per-instance
    /// floating-point color or an RGBA value that must preserve full
    /// float precision (HDR, accumulators).
    /// </summary>
    Float4,
    /// <summary>
    /// 4x4 float matrix (64 bytes). Consumes four consecutive shader
    /// attribute locations on the GPU side, one per row. Typical use is
    /// a per-instance world transform on an instance-rate vertex slot.
    /// </summary>
    Matrix4x4,
}

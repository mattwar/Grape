using System.Collections.Immutable;

namespace Grape;

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
    TextureCoordinate2,
    Color4
}

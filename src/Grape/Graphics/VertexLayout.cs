using System.Collections.Immutable;
using System.Linq;

namespace Grape;

/// <summary>
/// Describes the layout of vertices in a mesh.
/// </summary>
public sealed record VertexLayout(ImmutableArray<VertexElement> Elements)
{
    public VertexLayout(params VertexElementKind[] kinds)
        : this(kinds.Select(k => new VertexElement(k)).ToImmutableArray())
    {
    }
}

/// <summary>
/// Describes one element in a vertex layout.
/// </summary>
public sealed record VertexElement(VertexElementKind Kind);

/// <summary>
/// The kind of data represented by a vertex element.
/// </summary>
public enum VertexElementKind
{
    Position3,
    TextureCoordinate2,
    Color4
}

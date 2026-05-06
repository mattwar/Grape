namespace Grape;

/// <summary>
/// How consecutive vertices in a mesh are grouped into rendered shapes
/// (called "primitives"). The same vertex buffer means very different
/// pictures depending on the topology -- six vertices is two triangles
/// under <see cref="TriangleList"/>, or three line segments under
/// <see cref="LineList"/>, or four triangles under
/// <see cref="TriangleStrip"/>.
/// </summary>
public enum Topology
{
    /// <summary>
    /// Vertices in groups of three become independent triangles. The
    /// most common choice and the default for new meshes.
    /// <c>N</c> vertices produce <c>N / 3</c> triangles.
    /// </summary>
    TriangleList,

    /// <summary>
    /// Each new vertex forms a triangle with the previous two.
    /// Compact: <c>N</c> vertices produce <c>N - 2</c> triangles.
    /// Suited to ribbons, regular grids, and terrain rows.
    /// </summary>
    TriangleStrip,

    /// <summary>
    /// Vertices in pairs become independent line segments. The
    /// natural choice for debug gizmos, axis crosses, and wireframe
    /// edges. <c>N</c> vertices produce <c>N / 2</c> lines.
    /// </summary>
    LineList,

    /// <summary>
    /// Vertices form a continuous polyline; each new vertex extends
    /// from the previous. Useful for paths, function plots, and
    /// signal traces. <c>N</c> vertices produce <c>N - 1</c> lines.
    /// </summary>
    LineStrip,

    /// <summary>
    /// Each vertex is rendered as a single point. Useful for particle
    /// systems, debug markers, and point clouds.
    /// </summary>
    PointList,
}

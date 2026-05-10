using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Bounds and position-extraction helpers for <see cref="Mesh{TVertex}"/>
/// instances whose vertex type implements <see cref="IPositionVertex3D"/>
/// (which is every built-in 3D vertex type). For model-level bounds see
/// <see cref="ModelBounds"/>.
/// </summary>
public static class MeshBounds
{
    /// <summary>
    /// Copies vertex positions into a freshly-allocated array. Allocates;
    /// for hot paths use <see cref="WritePositionsTo{TVertex}"/>.
    /// </summary>
    public static Vector3[] GetPositions<TVertex>(this Mesh<TVertex> mesh)
        where TVertex : unmanaged, IPositionVertex3D
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var src = mesh.Vertices;
        var result = new Vector3[src.Length];
        for (int i = 0; i < src.Length; i++)
            result[i] = src[i].Position;
        return result;
    }

    /// <summary>
    /// Copies vertex positions into <paramref name="destination"/>, which
    /// must be at least <c>mesh.VertexCount</c> long. Returns the slice
    /// that was filled. Allocation-free.
    /// </summary>
    public static Span<Vector3> WritePositionsTo<TVertex>(this Mesh<TVertex> mesh, Span<Vector3> destination)
        where TVertex : unmanaged, IPositionVertex3D
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var src = mesh.Vertices;
        if (destination.Length < src.Length)
            throw new ArgumentException(
                $"Destination span ({destination.Length}) is too small for mesh vertex count ({src.Length}).",
                nameof(destination));
        for (int i = 0; i < src.Length; i++)
            destination[i] = src[i].Position;
        return destination[..src.Length];
    }

    /// <summary>
    /// Computes the AABB of <paramref name="mesh"/> in mesh-local space.
    /// Returns <see cref="BoundingBox.Empty"/> for a mesh with no vertices.
    /// </summary>
    public static BoundingBox ComputeBoundingBox<TVertex>(this Mesh<TVertex> mesh)
        where TVertex : unmanaged, IPositionVertex3D
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return BoundingBox.FromVertices(mesh.Vertices);
    }

    /// <summary>
    /// Computes a bounding sphere of <paramref name="mesh"/> in mesh-local
    /// space using the centroid + farthest-point heuristic.
    /// </summary>
    public static BoundingSphere ComputeBoundingSphere<TVertex>(this Mesh<TVertex> mesh)
        where TVertex : unmanaged, IPositionVertex3D
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return BoundingSphere.FromVertices(mesh.Vertices);
    }

    /// <summary>
    /// The AABB-center of <paramref name="mesh"/>. Equivalent to
    /// <c>mesh.ComputeBoundingBox().Center</c>; offered as a one-line
    /// shortcut because re-centering a loaded model is the most common
    /// reason to ask for it.
    /// </summary>
    public static Vector3 ComputeCenter<TVertex>(this Mesh<TVertex> mesh)
        where TVertex : unmanaged, IPositionVertex3D =>
        mesh.ComputeBoundingBox().Center;
}

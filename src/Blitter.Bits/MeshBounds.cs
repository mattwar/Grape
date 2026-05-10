using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Helpers to compute bounds for a <see cref="Mesh"/>
/// </summary>
public static class MeshBounds
{
    /// <summary>
    /// Computes the bounding box of the vertices in the <paramref name="mesh"/>.
    /// </summary>
    public static BoundingBox ComputeBoundingBox<TVertex>(this Mesh<TVertex> mesh)
        where TVertex : unmanaged, IPositionVertex3D
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return BoundingBox.FromVertices(mesh.Vertices);
    }

    /// <summary>
    /// Computes a bounding sphere of the vertices in the <paramref name="mesh"/>.
    /// </summary>
    public static BoundingSphere ComputeBoundingSphere<TVertex>(this Mesh<TVertex> mesh)
        where TVertex : unmanaged, IPositionVertex3D
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return BoundingSphere.FromVertices(mesh.Vertices);
    }

    /// <summary>
    /// Computes the center of the bounding box of the <paramref name="mesh"/>.
    /// </summary>
    public static Vector3 ComputeCenter<TVertex>(this Mesh<TVertex> mesh)
        where TVertex : unmanaged, IPositionVertex3D =>
        mesh.ComputeBoundingBox().Center;

    /// <summary>
    /// Returns a nominal set of bounding boxes that cover the surface of the <paramref name="mesh"/>.
    /// </summary>
    public static BoundingBox[] ComputeOccupiedBoxes<TVertex>(
        this Mesh<TVertex> mesh,
        float voxelSize,
        MeshOccupancyMode mode = MeshOccupancyMode.Accurate)
        where TVertex : unmanaged, IPositionVertex3D
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return MeshOccupancy.ComputeForMesh(mesh, voxelSize, mode);
    }
}

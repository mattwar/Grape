using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Strategy used to decide whether a voxel cell counts as "occupied" by a mesh
/// when computing a box-decomposition.
/// </summary>
public enum MeshOccupancyMode
{
    /// <summary>
    /// A voxel is occupied only if a triangle truly intersects it
    /// (separating-axis test). Tighter fit; slower per cell.
    /// </summary>
    Accurate,

    /// <summary>
    /// A voxel is occupied if it overlaps any triangle's own AABB. Cheap;
    /// can over-cover slanted triangles. Ideal for blocky, axis-aligned
    /// meshes where it gives the same answer as <see cref="Accurate"/>.
    /// </summary>
    Fast,
}

// Voxelization + 3D greedy-merge algorithm shared by the public
// ComputeOccupiedBoxes extensions on MeshBounds and ModelBounds.
internal static class MeshOccupancy
{
    public static BoundingBox[] ComputeForMesh<TVertex>(
        Mesh<TVertex> mesh,
        float voxelSize,
        MeshOccupancyMode mode)
        where TVertex : unmanaged, IPositionVertex3D
    {
        ValidateVoxelSize(voxelSize);
        if (mesh.Topology != Topology.TriangleList)
            throw new ArgumentException("Only TriangleList meshes are supported.", nameof(mesh));

        var bbox = mesh.ComputeBoundingBox();
        if (bbox.IsEmpty) return Array.Empty<BoundingBox>();

        var (origin, nx, ny, nz) = MakeGrid(bbox, voxelSize);
        var occupied = new bool[nx * ny * nz];

        VoxelizeMeshInto(mesh, occupied, nx, ny, nz, origin, voxelSize, mode);
        return GreedyMerge(occupied, nx, ny, nz, origin, voxelSize);
    }

    public static BoundingBox[] ComputeForModel(
        Model model,
        float voxelSize,
        MeshOccupancyMode mode)
    {
        ValidateVoxelSize(voxelSize);

        var bbox = model.ComputeBoundingBox();
        if (bbox.IsEmpty) return Array.Empty<BoundingBox>();

        var (origin, nx, ny, nz) = MakeGrid(bbox, voxelSize);
        var occupied = new bool[nx * ny * nz];

        foreach (var sub in model.Submeshes)
        {
            if (sub.Mesh.Topology != Topology.TriangleList) continue;
            VoxelizeMeshInto((Mesh<LitTextureVertex3D>)sub.Mesh, occupied, nx, ny, nz, origin, voxelSize, mode);
        }
        return GreedyMerge(occupied, nx, ny, nz, origin, voxelSize);
    }

    private static void ValidateVoxelSize(float voxelSize)
    {
        if (!(voxelSize > 0f) || float.IsInfinity(voxelSize))
            throw new ArgumentOutOfRangeException(nameof(voxelSize), "Must be a positive finite value.");
    }

    private static (Vector3 origin, int nx, int ny, int nz) MakeGrid(BoundingBox bbox, float voxelSize)
    {
        var size = bbox.Size;
        int nx = Math.Max(1, (int)MathF.Ceiling(size.X / voxelSize));
        int ny = Math.Max(1, (int)MathF.Ceiling(size.Y / voxelSize));
        int nz = Math.Max(1, (int)MathF.Ceiling(size.Z / voxelSize));
        return (bbox.Min, nx, ny, nz);
    }

    private static void VoxelizeMeshInto<TVertex>(
        Mesh<TVertex> mesh,
        bool[] occupied,
        int nx, int ny, int nz,
        Vector3 origin,
        float voxelSize,
        MeshOccupancyMode mode)
        where TVertex : unmanaged, IPositionVertex3D
    {
        var verts = mesh.Vertices;
        var indices = mesh.Indices;
        bool indexed = indices.Length > 0;
        int triCount = indexed ? indices.Length / 3 : verts.Length / 3;

        var halfExtents = new Vector3(voxelSize * 0.5f);
        float invVoxel = 1f / voxelSize;

        for (int t = 0; t < triCount; t++)
        {
            Vector3 a, b, c;
            if (indexed)
            {
                a = verts[(int)indices[t * 3]].Position;
                b = verts[(int)indices[t * 3 + 1]].Position;
                c = verts[(int)indices[t * 3 + 2]].Position;
            }
            else
            {
                a = verts[t * 3].Position;
                b = verts[t * 3 + 1].Position;
                c = verts[t * 3 + 2].Position;
            }

            var triMin = Vector3.Min(Vector3.Min(a, b), c) - origin;
            var triMax = Vector3.Max(Vector3.Max(a, b), c) - origin;

            int x0 = Math.Clamp((int)MathF.Floor(triMin.X * invVoxel), 0, nx - 1);
            int y0 = Math.Clamp((int)MathF.Floor(triMin.Y * invVoxel), 0, ny - 1);
            int z0 = Math.Clamp((int)MathF.Floor(triMin.Z * invVoxel), 0, nz - 1);
            int x1 = Math.Clamp((int)MathF.Floor(triMax.X * invVoxel), 0, nx - 1);
            int y1 = Math.Clamp((int)MathF.Floor(triMax.Y * invVoxel), 0, ny - 1);
            int z1 = Math.Clamp((int)MathF.Floor(triMax.Z * invVoxel), 0, nz - 1);

            for (int z = z0; z <= z1; z++)
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int idx = (z * ny + y) * nx + x;
                if (occupied[idx]) continue;

                if (mode == MeshOccupancyMode.Fast)
                {
                    occupied[idx] = true;
                }
                else
                {
                    var center = origin + new Vector3(
                        (x + 0.5f) * voxelSize,
                        (y + 0.5f) * voxelSize,
                        (z + 0.5f) * voxelSize);
                    if (TriangleIntersectsAabb(a, b, c, center, halfExtents))
                        occupied[idx] = true;
                }
            }
        }
    }

    // Akenine-Möller separating-axis test: triangle vs axis-aligned box.
    // Tests 3 box face normals, 1 triangle normal, and 9 edge×axis cross products.
    private static bool TriangleIntersectsAabb(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 center, Vector3 h)
    {
        var t0 = v0 - center;
        var t1 = v1 - center;
        var t2 = v2 - center;

        // Box face normals: project triangle onto X/Y/Z and check overlap with [-h, +h].
        var triMin = Vector3.Min(Vector3.Min(t0, t1), t2);
        var triMax = Vector3.Max(Vector3.Max(t0, t1), t2);
        if (triMin.X > h.X || triMax.X < -h.X) return false;
        if (triMin.Y > h.Y || triMax.Y < -h.Y) return false;
        if (triMin.Z > h.Z || triMax.Z < -h.Z) return false;

        // Triangle face normal: distance from box center (origin in this frame) to plane.
        var n = Vector3.Cross(t1 - t0, t2 - t0);
        float planeOffset = Vector3.Dot(n, t0);
        float boxRadius = h.X * MathF.Abs(n.X) + h.Y * MathF.Abs(n.Y) + h.Z * MathF.Abs(n.Z);
        if (MathF.Abs(planeOffset) > boxRadius) return false;

        // 9 edge × box-axis cross products.
        Span<Vector3> edges = stackalloc Vector3[3] { t1 - t0, t2 - t1, t0 - t2 };
        for (int i = 0; i < 3; i++)
        {
            var e = edges[i];
            // axis = e × X = (0, -e.Z, e.Y)
            if (!OverlapsOnAxis(new Vector3(0f, -e.Z, e.Y), t0, t1, t2, h)) return false;
            // axis = e × Y = (e.Z, 0, -e.X)
            if (!OverlapsOnAxis(new Vector3(e.Z, 0f, -e.X), t0, t1, t2, h)) return false;
            // axis = e × Z = (-e.Y, e.X, 0)
            if (!OverlapsOnAxis(new Vector3(-e.Y, e.X, 0f), t0, t1, t2, h)) return false;
        }
        return true;
    }

    private static bool OverlapsOnAxis(Vector3 axis, Vector3 t0, Vector3 t1, Vector3 t2, Vector3 h)
    {
        if (axis.LengthSquared() < 1e-20f) return true; // degenerate axis: not separating

        float p0 = Vector3.Dot(t0, axis);
        float p1 = Vector3.Dot(t1, axis);
        float p2 = Vector3.Dot(t2, axis);
        float pMin = MathF.Min(p0, MathF.Min(p1, p2));
        float pMax = MathF.Max(p0, MathF.Max(p1, p2));
        float r = h.X * MathF.Abs(axis.X) + h.Y * MathF.Abs(axis.Y) + h.Z * MathF.Abs(axis.Z);
        return !(pMin > r || pMax < -r);
    }

    // Greedy 3D merge: for each unconsumed occupied cell, extend X then Y then Z
    // as far as the resulting box stays fully occupied; emit the box and mark cells consumed.
    private static BoundingBox[] GreedyMerge(bool[] occupied, int nx, int ny, int nz, Vector3 origin, float voxelSize)
    {
        var result = new List<BoundingBox>();

        for (int z = 0; z < nz; z++)
        for (int y = 0; y < ny; y++)
        for (int x = 0; x < nx; x++)
        {
            if (!occupied[(z * ny + y) * nx + x]) continue;

            int w = 1;
            while (x + w < nx && occupied[(z * ny + y) * nx + (x + w)]) w++;

            int hCount = 1;
            while (y + hCount < ny && RowOccupied(occupied, nx, ny, x, y + hCount, z, w)) hCount++;

            int d = 1;
            while (z + d < nz && SlabOccupied(occupied, nx, ny, x, y, z + d, w, hCount)) d++;

            // Mark consumed.
            for (int kz = 0; kz < d; kz++)
            for (int ky = 0; ky < hCount; ky++)
            for (int kx = 0; kx < w; kx++)
                occupied[((z + kz) * ny + (y + ky)) * nx + (x + kx)] = false;

            var min = origin + new Vector3(x * voxelSize, y * voxelSize, z * voxelSize);
            var max = origin + new Vector3((x + w) * voxelSize, (y + hCount) * voxelSize, (z + d) * voxelSize);
            result.Add(new BoundingBox(min, max));
        }

        return result.ToArray();
    }

    private static bool RowOccupied(bool[] occupied, int nx, int ny, int x, int y, int z, int w)
    {
        int rowStart = (z * ny + y) * nx + x;
        for (int i = 0; i < w; i++)
            if (!occupied[rowStart + i]) return false;
        return true;
    }

    private static bool SlabOccupied(bool[] occupied, int nx, int ny, int x, int y, int z, int w, int h)
    {
        for (int j = 0; j < h; j++)
            if (!RowOccupied(occupied, nx, ny, x, y + j, z, w)) return false;
        return true;
    }
}

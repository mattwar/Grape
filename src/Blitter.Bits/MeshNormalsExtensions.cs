using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Mesh authoring helpers that compute per-vertex normals.
/// </summary>
public static class MeshNormalsExtensions
{
    /// <summary>
    /// Returns a new mesh whose normals have been recalculated from the triangle geometry. 
    /// When <paramref name="smooth"/> is <c>true</c> (the default), shared vertices receive an
    /// area-weighted average of their incident triangle normals -- good for organic surfaces. 
    /// When <c>false</c>, each triangle gets its own face normal -- good for hard-edged geometry;
    /// indexed meshes are expanded to unindexed in that case since shared vertices cannot carry per-face normals. 
    /// Only <see cref="Topology.TriangleList"/> is supported.
    /// </summary>
    public static Mesh<LitVertex3D> RecalculateNormals(this Mesh<LitVertex3D> mesh, bool smooth = true)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ValidateTriangleList(mesh.Topology, mesh.Vertices.Length, mesh.Indices.Length);

        if (!smooth) return FlatLit(mesh);

        var src = mesh.Vertices;
        var normals = ComputeSmoothNormals(src, mesh.Indices, p => p.Position);
        var dst = new LitVertex3D[src.Length];
        for (int i = 0; i < src.Length; i++)
            dst[i] = new LitVertex3D(src[i].Position, normals[i], src[i].Color);
        return Mesh.Create<LitVertex3D>(dst, mesh.Indices, mesh.Topology);
    }

    /// <inheritdoc cref="RecalculateNormals(Mesh{LitVertex3D}, bool)"/>
    public static Mesh<LitTextureVertex3D> RecalculateNormals(this Mesh<LitTextureVertex3D> mesh, bool smooth = true)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ValidateTriangleList(mesh.Topology, mesh.Vertices.Length, mesh.Indices.Length);

        if (!smooth) return FlatLitTextured(mesh);

        var src = mesh.Vertices;
        var normals = ComputeSmoothNormals(src, mesh.Indices, p => p.Position);
        var dst = new LitTextureVertex3D[src.Length];
        for (int i = 0; i < src.Length; i++)
            dst[i] = new LitTextureVertex3D(
                src[i].Position, normals[i], src[i].TextureCoordinate, src[i].Color);
        return Mesh.Create<LitTextureVertex3D>(dst, mesh.Indices, mesh.Topology);
    }

    private static void ValidateTriangleList(Topology topology, int vertexCount, int indexCount)
    {
        if (topology != Topology.TriangleList)
            throw new InvalidOperationException(
                $"RecalculateNormals is only supported for {Topology.TriangleList} (got {topology}).");
        var count = indexCount > 0 ? indexCount : vertexCount;
        if (count % 3 != 0)
            throw new InvalidOperationException(
                "Triangle list vertex/index count is not a multiple of 3.");
    }

    // Area-weighted accumulation: cross-product magnitude is 2x the
    // triangle area, which is the standard weighting for smooth
    // shading. For unindexed input we weld by exact position so
    // separately-listed vertices that share a point still share a
    // normal.
    private static Vector3[] ComputeSmoothNormals<TVertex>(
        ReadOnlySpan<TVertex> vertices,
        ReadOnlySpan<uint> indices,
        Func<TVertex, Vector3> getPosition)
        where TVertex : unmanaged
    {
        var normals = new Vector3[vertices.Length];

        if (indices.Length > 0)
        {
            for (int t = 0; t < indices.Length; t += 3)
            {
                uint ia = indices[t + 0];
                uint ib = indices[t + 1];
                uint ic = indices[t + 2];
                var pa = getPosition(vertices[(int)ia]);
                var pb = getPosition(vertices[(int)ib]);
                var pc = getPosition(vertices[(int)ic]);
                var n = Vector3.Cross(pb - pa, pc - pa);
                normals[ia] += n;
                normals[ib] += n;
                normals[ic] += n;
            }
        }
        else
        {
            // Weld by exact position so coincident vertices in the
            // unindexed buffer pick up the same smooth normal.
            var byPos = new Dictionary<Vector3, List<int>>(vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = getPosition(vertices[i]);
                if (!byPos.TryGetValue(p, out var list))
                {
                    list = new List<int>(1);
                    byPos[p] = list;
                }
                list.Add(i);
            }

            for (int t = 0; t < vertices.Length; t += 3)
            {
                var pa = getPosition(vertices[t + 0]);
                var pb = getPosition(vertices[t + 1]);
                var pc = getPosition(vertices[t + 2]);
                var n = Vector3.Cross(pb - pa, pc - pa);
                foreach (var i in byPos[pa]) normals[i] += n;
                foreach (var i in byPos[pb]) normals[i] += n;
                foreach (var i in byPos[pc]) normals[i] += n;
            }
        }

        for (int i = 0; i < normals.Length; i++)
        {
            var n = normals[i];
            var len = n.Length();
            normals[i] = len > 1e-20f ? n / len : Vector3.UnitY;
        }
        return normals;
    }

    private static Mesh<LitVertex3D> FlatLit(Mesh<LitVertex3D> mesh)
    {
        var src = mesh.Vertices;
        var indices = mesh.Indices;
        int triCount = (indices.Length > 0 ? indices.Length : src.Length) / 3;
        var dst = new LitVertex3D[triCount * 3];

        for (int t = 0; t < triCount; t++)
        {
            int o = t * 3;
            int ia = indices.Length > 0 ? (int)indices[o + 0] : o + 0;
            int ib = indices.Length > 0 ? (int)indices[o + 1] : o + 1;
            int ic = indices.Length > 0 ? (int)indices[o + 2] : o + 2;
            var va = src[ia];
            var vb = src[ib];
            var vc = src[ic];
            var n = Vector3.Cross(vb.Position - va.Position, vc.Position - va.Position);
            var len = n.Length();
            n = len > 1e-20f ? n / len : Vector3.UnitY;
            dst[o + 0] = new LitVertex3D(va.Position, n, va.Color);
            dst[o + 1] = new LitVertex3D(vb.Position, n, vb.Color);
            dst[o + 2] = new LitVertex3D(vc.Position, n, vc.Color);
        }
        // Drop indices; the flat-shaded buffer is intrinsically unindexed.
        return Mesh.Create<LitVertex3D>(dst, ReadOnlySpan<uint>.Empty, Topology.TriangleList);
    }

    private static Mesh<LitTextureVertex3D> FlatLitTextured(Mesh<LitTextureVertex3D> mesh)
    {
        var src = mesh.Vertices;
        var indices = mesh.Indices;
        int triCount = (indices.Length > 0 ? indices.Length : src.Length) / 3;
        var dst = new LitTextureVertex3D[triCount * 3];

        for (int t = 0; t < triCount; t++)
        {
            int o = t * 3;
            int ia = indices.Length > 0 ? (int)indices[o + 0] : o + 0;
            int ib = indices.Length > 0 ? (int)indices[o + 1] : o + 1;
            int ic = indices.Length > 0 ? (int)indices[o + 2] : o + 2;
            var va = src[ia];
            var vb = src[ib];
            var vc = src[ic];
            var n = Vector3.Cross(vb.Position - va.Position, vc.Position - va.Position);
            var len = n.Length();
            n = len > 1e-20f ? n / len : Vector3.UnitY;
            dst[o + 0] = new LitTextureVertex3D(va.Position, n, va.TextureCoordinate, va.Color);
            dst[o + 1] = new LitTextureVertex3D(vb.Position, n, vb.TextureCoordinate, vb.Color);
            dst[o + 2] = new LitTextureVertex3D(vc.Position, n, vc.TextureCoordinate, vc.Color);
        }
        return Mesh.Create<LitTextureVertex3D>(dst, ReadOnlySpan<uint>.Empty, Topology.TriangleList);
    }
}

namespace Blitter.Bits;

/// <summary>
/// Mesh authoring helpers that flip orientation
/// </summary>
public static class MeshFlipExtensions
{
    /// <summary>
    /// Returns a new mesh with every triangle's vertex order reversed.
    /// For an indexed <see cref="Topology.TriangleList"/> mesh this
    /// reorders the index buffer; for an unindexed one it reorders the
    /// vertices in groups of three. Strip topologies are not
    /// supported (would change the visible primitives).
    /// </summary>
    public static Mesh<TVertex> FlipWinding<TVertex>(this Mesh<TVertex> mesh)
        where TVertex : unmanaged
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.Topology != Topology.TriangleList)
            throw new InvalidOperationException(
                $"FlipWinding is only supported for {Topology.TriangleList} (got {mesh.Topology}).");

        var verts = mesh.Vertices;
        var indices = mesh.Indices;

        if (indices.Length > 0)
        {
            if (indices.Length % 3 != 0)
                throw new InvalidOperationException(
                    "Index count is not a multiple of 3.");
            var newIndices = new uint[indices.Length];
            for (int i = 0; i < indices.Length; i += 3)
            {
                newIndices[i + 0] = indices[i + 0];
                newIndices[i + 1] = indices[i + 2];
                newIndices[i + 2] = indices[i + 1];
            }
            return Mesh.Create<TVertex>(verts, newIndices, mesh.Topology);
        }

        if (verts.Length % 3 != 0)
            throw new InvalidOperationException(
                "Unindexed triangle list vertex count is not a multiple of 3.");
        var newVerts = new TVertex[verts.Length];
        for (int i = 0; i < verts.Length; i += 3)
        {
            newVerts[i + 0] = verts[i + 0];
            newVerts[i + 1] = verts[i + 2];
            newVerts[i + 2] = verts[i + 1];
        }
        return Mesh.Create<TVertex>(newVerts, ReadOnlySpan<uint>.Empty, mesh.Topology);
    }

    /// <summary>
    /// Returns a new mesh with every vertex normal negated. Use
    /// together with <see cref="FlipWinding"/> when turning a mesh
    /// inside-out so both the front-face determination and the lighting
    /// stay consistent.
    /// </summary>
    public static Mesh<LitVertex3D> FlipNormals(this Mesh<LitVertex3D> mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var src = mesh.Vertices;
        var dst = new LitVertex3D[src.Length];
        for (int i = 0; i < src.Length; i++)
            dst[i] = new LitVertex3D(src[i].Position, -src[i].Normal, src[i].Color);
        return Mesh.Create<LitVertex3D>(dst, mesh.Indices, mesh.Topology);
    }

    /// <inheritdoc cref="FlipNormals(Mesh{LitVertex3D})"/>
    public static Mesh<LitTextureVertex3D> FlipNormals(this Mesh<LitTextureVertex3D> mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var src = mesh.Vertices;
        var dst = new LitTextureVertex3D[src.Length];
        for (int i = 0; i < src.Length; i++)
            dst[i] = new LitTextureVertex3D(
                src[i].Position, -src[i].Normal, src[i].TextureCoordinate, src[i].Color);
        return Mesh.Create<LitTextureVertex3D>(dst, mesh.Indices, mesh.Topology);
    }
}

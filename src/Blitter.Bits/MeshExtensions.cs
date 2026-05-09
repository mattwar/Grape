using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Mesh authoring helpers: bake a transform into a mesh's vertices, or
/// concatenate two meshes into one. Intended for procedural mesh
/// composition (building a chair from a cube + cylinders, recentering a
/// loaded OBJ, mirroring a mesh, etc.). For per-frame motion use
/// <c>TransformArgs</c> at draw time instead -- baking transforms into
/// vertices every frame defeats the cached GPU buffer.
/// </summary>
public static class MeshExtensions
{
    // ---------------- Transform ----------------
    //
    // Positions go through the full 4x4. Normals go through the
    // inverse-transpose of the upper 3x3 and are renormalized so the
    // shader still sees unit vectors. For pure rotation + translation +
    // uniform scale the inverse-transpose collapses to the rotation
    // (after the renormalize), so the math is correct in both the easy
    // case and the non-uniform-scale case.

    public static Mesh<Vertex3D> Transform(this Mesh<Vertex3D> mesh, Matrix4x4 matrix)
    {
        var src = AsSpan(mesh);
        var dst = new Vertex3D[src.Length];
        for (int i = 0; i < src.Length; i++)
            dst[i] = new Vertex3D(Vector3.Transform(src[i].Position, matrix));
        return Mesh.Create<Vertex3D>(dst, mesh.Indices, mesh.Topology);
    }

    public static Mesh<ColorVertex3D> Transform(this Mesh<ColorVertex3D> mesh, Matrix4x4 matrix)
    {
        var src = AsSpan(mesh);
        var dst = new ColorVertex3D[src.Length];
        for (int i = 0; i < src.Length; i++)
            dst[i] = new ColorVertex3D(Vector3.Transform(src[i].Position, matrix), src[i].Color);
        return Mesh.Create<ColorVertex3D>(dst, mesh.Indices, mesh.Topology);
    }

    public static Mesh<TextureVertex3D> Transform(this Mesh<TextureVertex3D> mesh, Matrix4x4 matrix)
    {
        var src = AsSpan(mesh);
        var dst = new TextureVertex3D[src.Length];
        for (int i = 0; i < src.Length; i++)
            dst[i] = new TextureVertex3D(Vector3.Transform(src[i].Position, matrix), src[i].TextureCoordinate);
        return Mesh.Create<TextureVertex3D>(dst, mesh.Indices, mesh.Topology);
    }

    public static Mesh<LitVertex3D> Transform(this Mesh<LitVertex3D> mesh, Matrix4x4 matrix)
    {
        var src = AsSpan(mesh);
        var dst = new LitVertex3D[src.Length];
        var nm = NormalMatrix(matrix);
        for (int i = 0; i < src.Length; i++)
        {
            dst[i] = new LitVertex3D(
                Vector3.Transform(src[i].Position, matrix),
                Vector3.Normalize(Vector3.TransformNormal(src[i].Normal, nm)),
                src[i].Color);
        }
        return Mesh.Create<LitVertex3D>(dst, mesh.Indices, mesh.Topology);
    }

    public static Mesh<LitTextureVertex3D> Transform(this Mesh<LitTextureVertex3D> mesh, Matrix4x4 matrix)
    {
        var src = AsSpan(mesh);
        var dst = new LitTextureVertex3D[src.Length];
        var nm = NormalMatrix(matrix);
        for (int i = 0; i < src.Length; i++)
        {
            dst[i] = new LitTextureVertex3D(
                Vector3.Transform(src[i].Position, matrix),
                Vector3.Normalize(Vector3.TransformNormal(src[i].Normal, nm)),
                src[i].TextureCoordinate,
                src[i].Color);
        }
        return Mesh.Create<LitTextureVertex3D>(dst, mesh.Indices, mesh.Topology);
    }

    // ---------------- Concat ----------------
    //
    // Generic byte-level append. Both meshes must share topology.
    // Strip topologies are rejected because concatenating two strips
    // would form a spurious triangle / line connecting them.

    /// <summary>
    /// Returns a new mesh containing the vertices and primitives of
    /// <paramref name="a"/> followed by those of <paramref name="b"/>.
    /// Both meshes must share the same <see cref="Topology"/>; strip
    /// topologies are not supported.
    /// </summary>
    public static Mesh<TVertex> Concat<TVertex>(this Mesh<TVertex> a, Mesh<TVertex> b)
        where TVertex : unmanaged
    {
        ValidateConcat(a, b);
        var verts = new TVertex[a.VertexCount + b.VertexCount];
        AsSpan(a).CopyTo(verts);
        AsSpan(b).CopyTo(verts.AsSpan(a.VertexCount));
        return Mesh.Create<TVertex>(verts, BuildConcatIndices(a, b), a.Topology);
    }

    /// <summary>
    /// Convenience overload that bakes <paramref name="bTransform"/> into
    /// <paramref name="b"/>'s vertices while concatenating, so the
    /// transformed copy is never materialized as an intermediate mesh.
    /// </summary>
    public static Mesh<Vertex3D> Concat(this Mesh<Vertex3D> a, Mesh<Vertex3D> b, Matrix4x4 bTransform)
    {
        ValidateConcat(a, b);
        var verts = new Vertex3D[a.VertexCount + b.VertexCount];
        AsSpan(a).CopyTo(verts);
        var bSrc = AsSpan(b);
        for (int i = 0; i < bSrc.Length; i++)
            verts[a.VertexCount + i] = new Vertex3D(Vector3.Transform(bSrc[i].Position, bTransform));
        return Mesh.Create<Vertex3D>(verts, BuildConcatIndices(a, b), a.Topology);
    }

    public static Mesh<ColorVertex3D> Concat(this Mesh<ColorVertex3D> a, Mesh<ColorVertex3D> b, Matrix4x4 bTransform)
    {
        ValidateConcat(a, b);
        var verts = new ColorVertex3D[a.VertexCount + b.VertexCount];
        AsSpan(a).CopyTo(verts);
        var bSrc = AsSpan(b);
        for (int i = 0; i < bSrc.Length; i++)
            verts[a.VertexCount + i] = new ColorVertex3D(Vector3.Transform(bSrc[i].Position, bTransform), bSrc[i].Color);
        return Mesh.Create<ColorVertex3D>(verts, BuildConcatIndices(a, b), a.Topology);
    }

    public static Mesh<TextureVertex3D> Concat(this Mesh<TextureVertex3D> a, Mesh<TextureVertex3D> b, Matrix4x4 bTransform)
    {
        ValidateConcat(a, b);
        var verts = new TextureVertex3D[a.VertexCount + b.VertexCount];
        AsSpan(a).CopyTo(verts);
        var bSrc = AsSpan(b);
        for (int i = 0; i < bSrc.Length; i++)
            verts[a.VertexCount + i] = new TextureVertex3D(Vector3.Transform(bSrc[i].Position, bTransform), bSrc[i].TextureCoordinate);
        return Mesh.Create<TextureVertex3D>(verts, BuildConcatIndices(a, b), a.Topology);
    }

    public static Mesh<LitVertex3D> Concat(this Mesh<LitVertex3D> a, Mesh<LitVertex3D> b, Matrix4x4 bTransform)
    {
        ValidateConcat(a, b);
        var verts = new LitVertex3D[a.VertexCount + b.VertexCount];
        AsSpan(a).CopyTo(verts);
        var bSrc = AsSpan(b);
        var nm = NormalMatrix(bTransform);
        for (int i = 0; i < bSrc.Length; i++)
        {
            verts[a.VertexCount + i] = new LitVertex3D(
                Vector3.Transform(bSrc[i].Position, bTransform),
                Vector3.Normalize(Vector3.TransformNormal(bSrc[i].Normal, nm)),
                bSrc[i].Color);
        }
        return Mesh.Create<LitVertex3D>(verts, BuildConcatIndices(a, b), a.Topology);
    }

    public static Mesh<LitTextureVertex3D> Concat(this Mesh<LitTextureVertex3D> a, Mesh<LitTextureVertex3D> b, Matrix4x4 bTransform)
    {
        ValidateConcat(a, b);
        var verts = new LitTextureVertex3D[a.VertexCount + b.VertexCount];
        AsSpan(a).CopyTo(verts);
        var bSrc = AsSpan(b);
        var nm = NormalMatrix(bTransform);
        for (int i = 0; i < bSrc.Length; i++)
        {
            verts[a.VertexCount + i] = new LitTextureVertex3D(
                Vector3.Transform(bSrc[i].Position, bTransform),
                Vector3.Normalize(Vector3.TransformNormal(bSrc[i].Normal, nm)),
                bSrc[i].TextureCoordinate,
                bSrc[i].Color);
        }
        return Mesh.Create<LitTextureVertex3D>(verts, BuildConcatIndices(a, b), a.Topology);
    }

    // ---------------- helpers ----------------

    private static ReadOnlySpan<TVertex> AsSpan<TVertex>(Mesh<TVertex> mesh) where TVertex : unmanaged =>
        mesh.Vertices;

    private static void ValidateConcat<TVertex>(Mesh<TVertex> a, Mesh<TVertex> b) where TVertex : unmanaged
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Topology != b.Topology)
            throw new ArgumentException(
                $"Cannot Concat meshes with different topologies ({a.Topology} vs {b.Topology}).", nameof(b));
        if (a.Topology is Topology.TriangleStrip or Topology.LineStrip)
            throw new InvalidOperationException(
                $"Concat is not supported for {a.Topology} (would form a spurious connecting primitive).");
    }

    private static uint[] BuildConcatIndices<TVertex>(Mesh<TVertex> a, Mesh<TVertex> b) where TVertex : unmanaged
    {
        var aIdx = a.Indices;
        var bIdx = b.Indices;
        bool aIndexed = aIdx.Length > 0;
        bool bIndexed = bIdx.Length > 0;

        // Both unindexed: append vertices directly with no index buffer.
        if (!aIndexed && !bIndexed)
            return Array.Empty<uint>();

        // Otherwise emit indices for both halves; synthesize 0..N-1 for
        // any side that was unindexed.
        int aLen = aIndexed ? aIdx.Length : a.VertexCount;
        int bLen = bIndexed ? bIdx.Length : b.VertexCount;
        var result = new uint[aLen + bLen];

        if (aIndexed) aIdx.CopyTo(result);
        else for (int i = 0; i < aLen; i++) result[i] = (uint)i;

        uint offset = (uint)a.VertexCount;
        if (bIndexed)
            for (int i = 0; i < bLen; i++) result[aLen + i] = bIdx[i] + offset;
        else
            for (int i = 0; i < bLen; i++) result[aLen + i] = (uint)i + offset;

        return result;
    }

    private static Matrix4x4 NormalMatrix(Matrix4x4 m)
    {
        // Inverse-transpose of the model matrix correctly transforms
        // normals through non-uniform scale. For pure rotation /
        // translation / uniform scale the result, after renormalizing,
        // is equivalent to the rotation alone.
        if (!Matrix4x4.Invert(m, out var inv))
            return Matrix4x4.Identity;
        return Matrix4x4.Transpose(inv);
    }
}

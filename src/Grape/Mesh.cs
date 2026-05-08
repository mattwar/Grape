using System.Runtime.InteropServices;

namespace Grape;

/// <summary>
/// CPU-side mesh data used by the high-level renderer. Mesh instances are
/// compared by reference identity; reuse the same instance frame-to-frame to
/// reuse the cached GPU vertex buffer.
/// </summary>
public abstract class Mesh
{
    private protected Mesh() { }

    public abstract int VertexCount { get; }
    public abstract int IndexCount { get; }
    internal abstract ReadOnlySpan<byte> GetVertexBytes();
    public abstract ReadOnlySpan<uint> Indices { get; }

    /// <summary>
    /// How the mesh's vertices are grouped into rendered shapes
    /// (triangles, lines, points). Set at construction; immutable for
    /// the mesh's lifetime.
    /// </summary>
    public abstract Topology Topology { get; }

    /// <summary>
    /// Bumped each time the mesh's contents are replaced. The renderer uses
    /// this to detect when its cached GPU vertex buffer needs to be re-uploaded.
    /// </summary>
    public int Version { get; private protected set; }

    /// <summary>
    /// Creates a <see cref="Mesh{TVertex}"/> by copying the provided vertices.
    /// Convenient for collection expressions (e.g. <c>Mesh.Create([v0, v1, v2])</c>),
    /// which would otherwise be ambiguous between the span and immutable-array
    /// constructor overloads.
    /// </summary>
    public static Mesh<TVertex> Create<TVertex>(
        ReadOnlySpan<TVertex> vertices,
        Topology topology = Topology.TriangleList)
        where TVertex : unmanaged =>
        new Mesh<TVertex>(vertices, ReadOnlySpan<uint>.Empty, topology);

    /// <summary>
    /// Creates a <see cref="Mesh{TVertex}"/> by copying the provided vertices and indices.
    /// </summary>
    public static Mesh<TVertex> Create<TVertex>(
        ReadOnlySpan<TVertex> vertices,
        ReadOnlySpan<uint> indices,
        Topology topology = Topology.TriangleList)
        where TVertex : unmanaged =>
        new Mesh<TVertex>(vertices, indices, topology);
}

/// <summary>
/// CPU-side mesh data used by <see cref="Renderer3D"/>. Can be updated with
/// fresh vertex/index data via <see cref="Update(ReadOnlySpan{TVertex})"/>.
/// </summary>
public class Mesh<TVertex> : Mesh
    where TVertex : unmanaged
{
    private TVertex[] _vertices;
    private int _vertexCount;

    private uint[] _indices;
    private int _indexCount;

    internal Mesh(ReadOnlySpan<TVertex> vertices)
        : this(vertices, ReadOnlySpan<uint>.Empty, Topology.TriangleList)
    {
    }

    internal Mesh(
        ReadOnlySpan<TVertex> vertices,
        ReadOnlySpan<uint> indices,
        Topology topology = Topology.TriangleList)
    {
        _vertices = vertices.Length == 0 ? Array.Empty<TVertex>() : new TVertex[vertices.Length];
        vertices.CopyTo(_vertices);
        _vertexCount = vertices.Length;

        _indices = indices.Length == 0 ? Array.Empty<uint>() : new uint[indices.Length];
        indices.CopyTo(_indices);
        _indexCount = indices.Length;

        Topology = topology;
        Version = 1;
    }

    public override int VertexCount => _vertexCount;

    public override int IndexCount => _indexCount;

    public override Topology Topology { get; }

    /// <summary>
    /// Read-only view over the mesh's vertex data as the strongly-typed
    /// <typeparamref name="TVertex"/>. Cheap (no copy); the span is valid
    /// until the next <see cref="Update(ReadOnlySpan{TVertex})"/>.
    /// </summary>
    public ReadOnlySpan<TVertex> Vertices => _vertices.AsSpan(0, _vertexCount);

    internal override ReadOnlySpan<byte> GetVertexBytes() =>
        MemoryMarshal.AsBytes(_vertices.AsSpan(0, _vertexCount));

    public override ReadOnlySpan<uint> Indices =>
        _indices.AsSpan(0, _indexCount);

    /// <summary>
    /// Replaces the vertex data with new contents copied from the supplied
    /// span; the index buffer is left unchanged. Bumps <see cref="Mesh.Version"/>
    /// so the renderer re-uploads the GPU buffer on the next draw.
    /// </summary>
    public void Update(ReadOnlySpan<TVertex> vertices)
    {
        EnsureVertexCapacity(vertices.Length);
        vertices.CopyTo(_vertices);
        _vertexCount = vertices.Length;
        unchecked { Version++; }
    }

    /// <summary>
    /// Replaces both vertex and index data with new contents copied from the
    /// supplied spans. Bumps <see cref="Mesh.Version"/>.
    /// </summary>
    public void Update(ReadOnlySpan<TVertex> vertices, ReadOnlySpan<uint> indices)
    {
        EnsureVertexCapacity(vertices.Length);
        vertices.CopyTo(_vertices);
        _vertexCount = vertices.Length;

        EnsureIndexCapacity(indices.Length);
        indices.CopyTo(_indices);
        _indexCount = indices.Length;

        unchecked { Version++; }
    }

    private void EnsureVertexCapacity(int count)
    {
        if (_vertices.Length < count)
            _vertices = new TVertex[count];
    }

    private void EnsureIndexCapacity(int count)
    {
        if (_indices.Length < count)
            _indices = new uint[count];
    }
}

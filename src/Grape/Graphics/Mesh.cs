using System.Collections.Immutable;
using System.Runtime.CompilerServices;
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
    public abstract ReadOnlySpan<byte> GetVertexBytes();
    public abstract ReadOnlySpan<uint> GetIndices();

    /// <summary>
    /// Bumped each time the mesh's contents are replaced. The renderer uses
    /// this to detect when its cached GPU vertex buffer needs to be re-uploaded.
    /// </summary>
    public int Version { get; private protected set; }
}

/// <summary>
/// CPU-side mesh data used by <see cref="Renderer3D"/>. Can be updated with
/// fresh vertex/index data.
/// </summary>
public class Mesh<TVertex> : Mesh
    where TVertex : unmanaged
{
    // Either an array we own (writable) or one borrowed from an
    // ImmutableArray (must not be mutated). _ownsVertices/_ownsIndices
    // tells which.
    private TVertex[] _vertices;
    private int _vertexCount;
    private bool _ownsVertices;

    private uint[] _indices;
    private int _indexCount;
    private bool _ownsIndices;

    public Mesh(ReadOnlySpan<TVertex> vertices)
        : this(vertices, ReadOnlySpan<uint>.Empty)
    {
    }

    public Mesh(ImmutableArray<TVertex> vertices)
        : this(vertices, ImmutableArray<uint>.Empty)
    {
    }

    public Mesh(ReadOnlySpan<TVertex> vertices, ReadOnlySpan<uint> indices)
    {
        _vertices = vertices.Length == 0 ? Array.Empty<TVertex>() : new TVertex[vertices.Length];
        vertices.CopyTo(_vertices);
        _vertexCount = vertices.Length;
        _ownsVertices = true;

        _indices = indices.Length == 0 ? Array.Empty<uint>() : new uint[indices.Length];
        indices.CopyTo(_indices);
        _indexCount = indices.Length;
        _ownsIndices = true;

        Version = 1;
    }

    public Mesh(ImmutableArray<TVertex> vertices, ImmutableArray<uint> indices)
    {
        ThrowIfDefault(vertices, nameof(vertices));
        ThrowIfDefault(indices, nameof(indices));

        // Borrow the immutable arrays' backing storage directly. Immutable
        // arrays guarantee their contents won't change, so we can safely
        // read from them without copying.
        _vertices = ImmutableCollectionsMarshal.AsArray(vertices) ?? Array.Empty<TVertex>();
        _vertexCount = vertices.Length;
        _ownsVertices = false;

        _indices = ImmutableCollectionsMarshal.AsArray(indices) ?? Array.Empty<uint>();
        _indexCount = indices.Length;
        _ownsIndices = false;

        Version = 1;
    }

    public override int VertexCount => _vertexCount;

    public override int IndexCount => _indexCount;

    public override ReadOnlySpan<byte> GetVertexBytes() =>
        MemoryMarshal.AsBytes(_vertices.AsSpan(0, _vertexCount));

    public override ReadOnlySpan<uint> GetIndices() =>
        _indices.AsSpan(0, _indexCount);

    /// <summary>
    /// Replaces the vertex and index data with new contents copied from the
    /// supplied spans. Bumps <see cref="Mesh.Version"/> so the renderer
    /// re-uploads the GPU buffer on the next draw.
    /// </summary>
    public void Reset(ReadOnlySpan<TVertex> vertices, ReadOnlySpan<uint> indices)
    {
        EnsureOwnedVertexCapacity(vertices.Length);
        vertices.CopyTo(_vertices);
        _vertexCount = vertices.Length;

        EnsureOwnedIndexCapacity(indices.Length);
        indices.CopyTo(_indices);
        _indexCount = indices.Length;

        unchecked { Version++; }
    }

    /// <summary>
    /// Replaces the vertex and index data with the supplied immutable arrays.
    /// The arrays' underlying storage is borrowed in-place (zero copy).
    /// Bumps <see cref="Mesh.Version"/>.
    /// </summary>
    public void Reset(ImmutableArray<TVertex> vertices, ImmutableArray<uint> indices)
    {
        ThrowIfDefault(vertices, nameof(vertices));
        ThrowIfDefault(indices, nameof(indices));

        _vertices = ImmutableCollectionsMarshal.AsArray(vertices) ?? Array.Empty<TVertex>();
        _vertexCount = vertices.Length;
        _ownsVertices = false;

        _indices = ImmutableCollectionsMarshal.AsArray(indices) ?? Array.Empty<uint>();
        _indexCount = indices.Length;
        _ownsIndices = false;

        unchecked { Version++; }
    }

    private void EnsureOwnedVertexCapacity(int count)
    {
        if (!_ownsVertices || _vertices.Length < count)
        {
            // Either we're holding a borrowed (immutable) array we mustn't
            // overwrite, or the owned array is too small. Allocate fresh.
            _vertices = count == 0 ? Array.Empty<TVertex>() : new TVertex[count];
            _ownsVertices = true;
        }
    }

    private void EnsureOwnedIndexCapacity(int count)
    {
        if (!_ownsIndices || _indices.Length < count)
        {
            _indices = count == 0 ? Array.Empty<uint>() : new uint[count];
            _ownsIndices = true;
        }
    }

    private static void ThrowIfDefault<T>(ImmutableArray<T> array, string paramName)
    {
        if (array.IsDefault)
            throw new ArgumentException("ImmutableArray must be initialised.", paramName);
    }
}

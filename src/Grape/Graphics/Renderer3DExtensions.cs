using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Grape;

/// <summary>
/// Additional overloads for <see cref="Renderer3D"/> that provide convenient defaults.
/// </summary>
public static class Renderer3DExtensions
{
    private static readonly ConditionalWeakTable<Renderer3D, ConditionalWeakTable<Array, Mesh>> Caches = new();

    private static ConditionalWeakTable<Array, Mesh> GetCache(Renderer3D renderer)
        => Caches.GetValue(renderer, static _ => new ConditionalWeakTable<Array, Mesh>());

    /// <summary>
    /// Returns a cached <see cref="Mesh{TVertex}"/> wrapping the supplied
    /// array. The first <paramref name="count"/> elements are pushed into
    /// the mesh on every call; subsequent identical contents are detected
    /// and not re-uploaded by the renderer's own version-tracking.
    /// </summary>
    private static Mesh<TVertex> GetOrCreateMesh<TVertex>(this Renderer3D renderer, TVertex[] vertices, int count)
        where TVertex : unmanaged
    {
        var cache = GetCache(renderer);
        var span = vertices.AsSpan(0, count);

        if (cache.TryGetValue(vertices, out var existing))
        {
            if (existing is not Mesh<TVertex> typed)
                throw new ArgumentException(
                    $"Vertex array was previously used with vertex type " +
                    $"'{existing!.GetType().GetGenericArguments().FirstOrDefault()?.Name ?? existing.GetType().Name}' " +
                    $"and cannot be reused with '{typeof(TVertex).Name}'.",
                    nameof(vertices));

            // Re-stage the latest contents. Reset bumps the mesh's Version,
            // and the upload loop only re-uploads when Version has changed.
            typed.Reset(span, ReadOnlySpan<uint>.Empty);
            return typed;
        }

        var mesh = new Mesh<TVertex>(span, ReadOnlySpan<uint>.Empty);
        cache.Add(vertices, mesh);
        return mesh;
    }

    /// <summary>
    /// Returns a cached <see cref="Mesh{TVertex}"/> wrapping the supplied
    /// <see cref="ImmutableArray{T}"/>. The mesh borrows the array's
    /// backing storage zero-copy.
    /// </summary>
    private static Mesh<TVertex> GetOrCreateMesh<TVertex>(this Renderer3D renderer, ImmutableArray<TVertex> vertices)
        where TVertex : unmanaged
    {
        var cache = GetCache(renderer);

        // Key on the immutable array's underlying T[]. Two ImmutableArrays
        // built over the same backing array hit the same cache entry, which
        // is fine since neither can mutate.
        var backing = ImmutableCollectionsMarshal.AsArray(vertices) ?? Array.Empty<TVertex>();

        if (cache.TryGetValue(backing, out var existing))
        {
            if (existing is not Mesh<TVertex> typed)
                throw new ArgumentException(
                    $"Vertex array was previously used with vertex type " +
                    $"'{existing!.GetType().GetGenericArguments().FirstOrDefault()?.Name ?? existing.GetType().Name}' " +
                    $"and cannot be reused with '{typeof(TVertex).Name}'.",
                    nameof(vertices));

            // Immutable: contents can't have changed, so no Reset needed.
            return typed;
        }

        // Borrow the backing array zero-copy via the ImmutableArray ctor.
        var mesh = new Mesh<TVertex>(vertices, ImmutableArray<uint>.Empty);
        cache.Add(backing, mesh);
        return mesh;
    }

    // ---- Shader-defaulting Mesh overloads ----------------------------------

    /// <summary>Draws a position-only mesh.</summary>
    public static void DrawMesh(this Renderer3D renderer, Mesh<Vertex3D> mesh)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawMesh(mesh, Shaders.Position);
    }

    /// <summary>Draws a position-only mesh with the given position transform.</summary>
    public static void DrawMesh(this Renderer3D renderer, Mesh<Vertex3D> mesh, in Matrix4x4 transform)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawMesh(mesh, Shaders.PositionWithTransform, in transform);
    }

    /// <summary>Draws a position &amp; color mesh.</summary>
    public static void DrawMesh(this Renderer3D renderer, Mesh<ColorVertex3D> mesh)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawMesh(mesh, Shaders.PositionColor);
    }

    /// <summary>Draws a position &amp; color mesh with the given position transform.</summary>
    public static void DrawMesh(this Renderer3D renderer, Mesh<ColorVertex3D> mesh, in Matrix4x4 transform)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawMesh(mesh, Shaders.PositionColorWithTransform, in transform);
    }

    /// <summary>Draws a position &amp; texture mesh with the given texture.</summary>
    public static void DrawMesh(this Renderer3D renderer, Mesh<TextureVertex3D> mesh, Image texture)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawMesh(mesh, texture, Shaders.PositionTexture);
    }

    /// <summary>Draws a position &amp; texture mesh with the given texture and position transform.</summary>
    public static void DrawMesh(this Renderer3D renderer, Mesh<TextureVertex3D> mesh, Image texture, in Matrix4x4 transform)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawMesh(mesh, texture, Shaders.PositionTextureWithTransform, in transform);
    }

    // ---- Plain (non-textured) array overloads ------------------------------

    /// <summary>
    /// Draws a mesh sourced directly from a caller-owned array.
    /// </summary>
    public static void DrawMesh<TVertex>(
        this Renderer3D renderer,
        TVertex[] vertices,
        ShaderSet<TVertex> shader,
        int? vertexCount = null)
        where TVertex : unmanaged
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(shader);

        var count = vertexCount ?? vertices.Length;
        if ((uint)count > (uint)vertices.Length)
            throw new ArgumentOutOfRangeException(nameof(vertexCount));

        var mesh = renderer.GetOrCreateMesh(vertices, count);
        renderer.DrawMesh(mesh, shader);
    }

    /// <summary>
    /// Draws a mesh from a caller-owned array using a shader that takes a
    /// typed per-draw arguments value.
    /// </summary>
    public static void DrawMesh<TVertex, TArgs>(
        this Renderer3D renderer,
        TVertex[] vertices,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args,
        int? vertexCount = null)
        where TVertex : unmanaged
        where TArgs : unmanaged
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(shader);

        var count = vertexCount ?? vertices.Length;
        if ((uint)count > (uint)vertices.Length)
            throw new ArgumentOutOfRangeException(nameof(vertexCount));

        var mesh = renderer.GetOrCreateMesh(vertices, count);
        renderer.DrawMesh(mesh, shader, in args);
    }

    /// <summary>
    /// Draws a mesh sourced from an <see cref="ImmutableArray{T}"/>.
    /// </summary>
    public static void DrawMesh<TVertex>(
        this Renderer3D renderer,
        ImmutableArray<TVertex> vertices,
        ShaderSet<TVertex> shader)
        where TVertex : unmanaged
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(shader);
        if (vertices.IsDefault)
            throw new ArgumentException("ImmutableArray must be initialised.", nameof(vertices));

        var mesh = renderer.GetOrCreateMesh(vertices);
        renderer.DrawMesh(mesh, shader);
    }

    /// <summary>
    /// Draws a mesh from an <see cref="ImmutableArray{T}"/> using a shader
    /// that takes a typed per-draw arguments value.
    /// </summary>
    public static void DrawMesh<TVertex, TArgs>(
        this Renderer3D renderer,
        ImmutableArray<TVertex> vertices,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args)
        where TVertex : unmanaged
        where TArgs : unmanaged
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(shader);
        if (vertices.IsDefault)
            throw new ArgumentException("ImmutableArray must be initialised.", nameof(vertices));

        var mesh = renderer.GetOrCreateMesh(vertices);
        renderer.DrawMesh(mesh, shader, in args);
    }

    // ---- Textured array overloads ------------------------------------------

    /// <summary>Draws a textured mesh from a caller-owned vertex array.</summary>
    public static void DrawMesh<TVertex>(
        this Renderer3D renderer,
        TVertex[] vertices,
        Image texture,
        ShaderSet<TVertex> shader,
        int? vertexCount = null)
        where TVertex : unmanaged
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(texture);

        var count = vertexCount ?? vertices.Length;
        if ((uint)count > (uint)vertices.Length)
            throw new ArgumentOutOfRangeException(nameof(vertexCount));

        var mesh = renderer.GetOrCreateMesh(vertices, count);
        renderer.DrawMesh(mesh, texture, shader);
    }

    /// <summary>
    /// Draws a textured mesh from a caller-owned vertex array using a shader
    /// with typed per-draw args.
    /// </summary>
    public static void DrawMesh<TVertex, TArgs>(
        this Renderer3D renderer,
        TVertex[] vertices,
        Image texture,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args,
        int? vertexCount = null)
        where TVertex : unmanaged
        where TArgs : unmanaged
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(texture);

        var count = vertexCount ?? vertices.Length;
        if ((uint)count > (uint)vertices.Length)
            throw new ArgumentOutOfRangeException(nameof(vertexCount));

        var mesh = renderer.GetOrCreateMesh(vertices, count);
        renderer.DrawMesh(mesh, texture, shader, in args);
    }

    /// <summary>Draws a textured mesh from an <see cref="ImmutableArray{T}"/>.</summary>
    public static void DrawMesh<TVertex>(
        this Renderer3D renderer,
        ImmutableArray<TVertex> vertices,
        Image texture,
        ShaderSet<TVertex> shader)
        where TVertex : unmanaged
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(texture);
        if (vertices.IsDefault)
            throw new ArgumentException("ImmutableArray must be initialised.", nameof(vertices));

        var mesh = renderer.GetOrCreateMesh(vertices);
        renderer.DrawMesh(mesh, texture, shader);
    }

    /// <summary>
    /// Draws a textured mesh from an <see cref="ImmutableArray{T}"/> using a
    /// shader with typed per-draw args.
    /// </summary>
    public static void DrawMesh<TVertex, TArgs>(
        this Renderer3D renderer,
        ImmutableArray<TVertex> vertices,
        Image texture,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args)
        where TVertex : unmanaged
        where TArgs : unmanaged
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(texture);
        if (vertices.IsDefault)
            throw new ArgumentException("ImmutableArray must be initialised.", nameof(vertices));

        var mesh = renderer.GetOrCreateMesh(vertices);
        renderer.DrawMesh(mesh, texture, shader, in args);
    }

    // ---- Shader-defaulting array overloads ---------------------------------

    /// <summary>Draws a position-only mesh from an array.</summary>
    public static void DrawMesh(this Renderer3D renderer, Vertex3D[] vertices, int? vertexCount = null)
        => renderer.DrawMesh(vertices, Shaders.Position, vertexCount);

    /// <summary>Draws a position-only mesh from an array with the given position transform.</summary>
    public static void DrawMesh(this Renderer3D renderer, Vertex3D[] vertices, in Matrix4x4 transform, int? vertexCount = null)
        => renderer.DrawMesh(vertices, Shaders.PositionWithTransform, in transform, vertexCount);

    /// <summary>Draws a position &amp; color mesh from an array.</summary>
    public static void DrawMesh(this Renderer3D renderer, ColorVertex3D[] vertices, int? vertexCount = null)
        => renderer.DrawMesh(vertices, Shaders.PositionColor, vertexCount);

    /// <summary>Draws a position &amp; color mesh from an array with the given position transform.</summary>
    public static void DrawMesh(this Renderer3D renderer, ColorVertex3D[] vertices, in Matrix4x4 transform, int? vertexCount = null)
        => renderer.DrawMesh(vertices, Shaders.PositionColorWithTransform, in transform, vertexCount);

    /// <summary>Draws a position &amp; texture mesh from an array with the given texture.</summary>
    public static void DrawMesh(this Renderer3D renderer, TextureVertex3D[] vertices, Image texture, int? vertexCount = null)
        => renderer.DrawMesh(vertices, texture, Shaders.PositionTexture, vertexCount);

    /// <summary>Draws a position &amp; texture mesh from an array with the given texture and position transform.</summary>
    public static void DrawMesh(this Renderer3D renderer, TextureVertex3D[] vertices, Image texture, in Matrix4x4 transform, int? vertexCount = null)
        => renderer.DrawMesh(vertices, texture, Shaders.PositionTextureWithTransform, in transform, vertexCount);

    // ---- Shader-defaulting ImmutableArray overloads ------------------------

    /// <summary>Draws a position-only mesh from an immutable array.</summary>
    public static void DrawMesh(this Renderer3D renderer, ImmutableArray<Vertex3D> vertices)
        => renderer.DrawMesh(vertices, Shaders.Position);

    /// <summary>Draws a position-only mesh from an immutable array with the given position transform.</summary>
    public static void DrawMesh(this Renderer3D renderer, ImmutableArray<Vertex3D> vertices, in Matrix4x4 transform)
        => renderer.DrawMesh(vertices, Shaders.PositionWithTransform, in transform);

    /// <summary>Draws a position &amp; color mesh from an immutable array.</summary>
    public static void DrawMesh(this Renderer3D renderer, ImmutableArray<ColorVertex3D> vertices)
        => renderer.DrawMesh(vertices, Shaders.PositionColor);

    /// <summary>Draws a position &amp; color mesh from an immutable array with the given position transform.</summary>
    public static void DrawMesh(this Renderer3D renderer, ImmutableArray<ColorVertex3D> vertices, in Matrix4x4 transform)
        => renderer.DrawMesh(vertices, Shaders.PositionColorWithTransform, in transform);

    /// <summary>Draws a position &amp; texture mesh from an immutable array with the given texture.</summary>
    public static void DrawMesh(this Renderer3D renderer, ImmutableArray<TextureVertex3D> vertices, Image texture)
        => renderer.DrawMesh(vertices, texture, Shaders.PositionTexture);

    /// <summary>Draws a position &amp; texture mesh from an immutable array with the given texture and position transform.</summary>
    public static void DrawMesh(this Renderer3D renderer, ImmutableArray<TextureVertex3D> vertices, Image texture, in Matrix4x4 transform)
        => renderer.DrawMesh(vertices, texture, Shaders.PositionTextureWithTransform, in transform);
}

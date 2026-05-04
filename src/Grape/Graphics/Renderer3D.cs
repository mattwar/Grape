using System.Collections.Immutable;
using System.Numerics;

namespace Grape;

/// <summary>
/// A renderer that renders 3D graphics to a target.
/// </summary>
public abstract class Renderer3D
{
    /// <summary>Lazy access to the precompiled shaders bundled with Grape.</summary>
    public abstract BuiltInShaders Shaders { get; }

    /// <summary>Draws a mesh using the given shader.</summary>
    public abstract void RenderMesh<TVertex>(Mesh<TVertex> mesh, Shader<TVertex> shader)
        where TVertex : unmanaged;

    /// <summary>
    /// Draws a mesh using a shader that takes a typed per-draw arguments
    /// value. The bytes of <paramref name="args"/> are split across
    /// stage/slot pairs as described by
    /// <see cref="Shader{TVertex,TArgs}.UniformLayout"/>.
    /// </summary>
    public abstract void RenderMesh<TVertex, TArgs>(
        Mesh<TVertex> mesh,
        Shader<TVertex, TArgs> shader,
        in TArgs args)
        where TVertex : unmanaged
        where TArgs : unmanaged;

    /// <summary>
    /// Draws a mesh sourced directly from a caller-owned array; the renderer
    /// keeps a weak association between the array reference and an internal
    /// <see cref="Mesh{TVertex}"/> so subsequent frames reuse the cached
    /// GPU buffer when contents have not changed.
    /// </summary>
    public abstract void RenderMesh<TVertex>(TVertex[] vertices, Shader<TVertex> shader, int? vertexCount = null)
        where TVertex : unmanaged;

    /// <summary>
    /// Draws a mesh from a caller-owned array using a shader that takes a
    /// typed per-draw arguments value.
    /// </summary>
    public abstract void RenderMesh<TVertex, TArgs>(
        TVertex[] vertices,
        Shader<TVertex, TArgs> shader,
        in TArgs args,
        int? vertexCount = null)
        where TVertex : unmanaged
        where TArgs : unmanaged;

    /// <summary>
    /// Draws a mesh sourced from an <see cref="ImmutableArray{T}"/>; the
    /// renderer borrows the array's backing storage zero-copy.
    /// </summary>
    public abstract void RenderMesh<TVertex>(ImmutableArray<TVertex> vertices, Shader<TVertex> shader)
        where TVertex : unmanaged;

    /// <summary>
    /// Draws a mesh from an <see cref="ImmutableArray{T}"/> using a shader
    /// that takes a typed per-draw arguments value.
    /// </summary>
    public abstract void RenderMesh<TVertex, TArgs>(
        ImmutableArray<TVertex> vertices,
        Shader<TVertex, TArgs> shader,
        in TArgs args)
        where TVertex : unmanaged
        where TArgs : unmanaged;

    /// <summary>Draws a textured mesh sampling from the given image.</summary>
    public abstract void RenderTexturedMesh(
        Mesh<TextureVertex3D> mesh,
        Shader<TextureVertex3D> shader,
        Image texture);

    /// <summary>Draws a textured mesh using a shader with typed per-draw args.</summary>
    public abstract void RenderTexturedMesh<TArgs>(
        Mesh<TextureVertex3D> mesh,
        Shader<TextureVertex3D, TArgs> shader,
        Image texture,
        in TArgs args)
        where TArgs : unmanaged;

    /// <summary>Draws a textured mesh from a caller-owned vertex array.</summary>
    public abstract void RenderTexturedMesh(
        TextureVertex3D[] vertices,
        Shader<TextureVertex3D> shader,
        Image texture,
        int? vertexCount = null);

    /// <summary>
    /// Draws a textured mesh from a caller-owned vertex array using a shader
    /// with typed per-draw args.
    /// </summary>
    public abstract void RenderTexturedMesh<TArgs>(
        TextureVertex3D[] vertices,
        Shader<TextureVertex3D, TArgs> shader,
        Image texture,
        in TArgs args,
        int? vertexCount = null)
        where TArgs : unmanaged;

    /// <summary>Draws a textured mesh from an <see cref="ImmutableArray{T}"/>.</summary>
    public abstract void RenderTexturedMesh(
        ImmutableArray<TextureVertex3D> vertices,
        Shader<TextureVertex3D> shader,
        Image texture);

    /// <summary>
    /// Draws a textured mesh from an <see cref="ImmutableArray{T}"/> using a
    /// shader with typed per-draw args.
    /// </summary>
    public abstract void RenderTexturedMesh<TArgs>(
        ImmutableArray<TextureVertex3D> vertices,
        Shader<TextureVertex3D, TArgs> shader,
        Image texture,
        in TArgs args)
        where TArgs : unmanaged;

    /// <summary>Renders ASCII debug text at the given world-space transform.</summary>
    public abstract void RenderDebugText(string text, Matrix4x4 transform);
}

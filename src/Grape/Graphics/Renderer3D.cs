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
    public abstract void RenderMesh<TVertex>(Mesh<TVertex> mesh, Shader<TVertex> shader, Matrix4x4? transform = null)
        where TVertex : unmanaged;

    /// <summary>
    /// Draws a mesh sourced directly from a caller-owned array; the renderer
    /// keeps a weak association between the array reference and an internal
    /// <see cref="Mesh{TVertex}"/> so subsequent frames reuse the cached
    /// GPU buffer when contents have not changed.
    /// </summary>
    public abstract void RenderMesh<TVertex>(TVertex[] vertices, Shader<TVertex> shader, Matrix4x4? transform = null, int? vertexCount = null)
        where TVertex : unmanaged;

    /// <summary>
    /// Draws a mesh sourced from an <see cref="ImmutableArray{T}"/>; the
    /// renderer borrows the array's backing storage zero-copy.
    /// </summary>
    public abstract void RenderMesh<TVertex>(ImmutableArray<TVertex> vertices, Shader<TVertex> shader, Matrix4x4? transform = null)
        where TVertex : unmanaged;

    /// <summary>Draws a textured mesh sampling from the given image.</summary>
    public abstract void RenderTexturedMesh(
        Mesh<TextureVertex3D> mesh,
        Shader<TextureVertex3D> shader,
        Image texture,
        Matrix4x4? transform = null);

    /// <summary>Draws a textured mesh from a caller-owned vertex array.</summary>
    public abstract void RenderTexturedMesh(
        TextureVertex3D[] vertices,
        Shader<TextureVertex3D> shader,
        Image texture,
        Matrix4x4? transform = null,
        int? vertexCount = null);

    /// <summary>Draws a textured mesh from an <see cref="ImmutableArray{T}"/>.</summary>
    public abstract void RenderTexturedMesh(
        ImmutableArray<TextureVertex3D> vertices,
        Shader<TextureVertex3D> shader,
        Image texture,
        Matrix4x4? transform = null);

    /// <summary>Renders ASCII debug text at the given world-space transform.</summary>
    public abstract void RenderDebugText(string text, Matrix4x4 transform);
}

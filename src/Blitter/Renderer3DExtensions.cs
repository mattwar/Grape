
namespace Blitter;

/// <summary>
/// Convenience overloads for <see cref="Renderer3D"/> that pick a sensible
/// built-in shader based on the mesh's vertex type. For arbitrary vertex
/// types or custom shaders, call <see cref="Renderer3D.DrawMesh{TVertex}(Mesh{TVertex},
/// Shader{TVertex})"/> directly.
/// </summary>
public static class Renderer3DExtensions
{
    /// <summary>Draws a position-only mesh.</summary>
    public static void DrawMesh(this Renderer3D renderer, Mesh<Vertex3D> mesh)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawMesh(mesh, Shaders.Position);
    }

    /// <summary>Draws a position-only mesh with the given position transform.</summary>
    public static void DrawMesh(this Renderer3D renderer, Mesh<Vertex3D> mesh, TransformArgs transform)
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
    public static void DrawMesh(this Renderer3D renderer, Mesh<ColorVertex3D> mesh, TransformArgs transform)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawMesh(mesh, Shaders.PositionColorWithTransform, in transform);
    }

    /// <summary>Draws a position &amp; texture mesh with the given texture.</summary>
    public static void DrawMesh(this Renderer3D renderer, Mesh<TextureVertex3D> mesh, Texture2D texture)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawMesh(mesh, texture, Shaders.PositionTexture);
    }

    /// <summary>Draws a position &amp; texture mesh with the given texture and position transform.</summary>
    public static void DrawMesh(this Renderer3D renderer, Mesh<TextureVertex3D> mesh, Texture2D texture, TransformArgs transform)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawMesh(mesh, texture, Shaders.PositionTextureWithTransform, in transform);
    }
}

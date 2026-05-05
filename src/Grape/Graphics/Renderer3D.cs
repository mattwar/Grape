using System.Numerics;

namespace Grape;

/// <summary>
/// A renderer that renders 3D graphics to a target.
/// </summary>
public abstract class Renderer3D
{
    /// <summary>
    /// Renders a mesh using a compatible <see cref="ShaderSet{TVertex}"/>.
    /// </summary>
    public abstract void RenderMesh<TVertex>(Mesh<TVertex> mesh, ShaderSet<TVertex> shader)
        where TVertex : unmanaged;

    /// <summary>
    /// Renders a mesh using a compatible <see cref="ShaderSet{TVertex,TArgs}"/> with the given per-draw arguments.
    /// </summary>
    public abstract void RenderMesh<TVertex, TArgs>(
        Mesh<TVertex> mesh,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args)
        where TVertex : unmanaged
        where TArgs : unmanaged;

    /// <summary>Draws a mesh sampling from the given image.</summary>
    public abstract void RenderMesh<TVertex>(
        Mesh<TVertex> mesh,
        Image texture,
        ShaderSet<TVertex> shader)
        where TVertex : unmanaged;

    /// <summary>Draws a textured mesh using a shader with typed per-draw args.</summary>
    public abstract void RenderMesh<TVertex, TArgs>(
        Mesh<TVertex> mesh,
        Image texture,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args)
        where TVertex : unmanaged
        where TArgs : unmanaged;

    /// <summary>Renders ASCII debug text at the given world-space transform.</summary>
    public abstract void RenderDebugText(string text, in Matrix4x4 transform);
}

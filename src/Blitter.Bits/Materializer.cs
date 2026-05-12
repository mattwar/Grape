using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Draws meshes using a shader determined by the material.
/// </summary>
public abstract class Materializer
{
    /// <summary>
    /// Material used by the <c>Renderer3D.DrawMesh(mesh, ...)</c>
    /// extension overloads that omit the material argument. The base
    /// implementation throws; subclasses opt in by overriding when
    /// they have a sensible "no-config" material for their shading
    /// policy.
    /// </summary>
    public virtual Material DefaultMaterial
        => throw new InvalidOperationException(
            $"{GetType().Name} does not provide a DefaultMaterial; " +
            "pass an explicit material to DrawMesh / DrawModel.");

    /// <summary>
    /// Draws <paramref name="mesh"/> on <paramref name="renderer"/>
    /// using the shader the subclass picks for
    /// <paramref name="material"/>'s kind. Throws
    /// <see cref="MaterializerNotSupportedException"/> if the subclass
    /// has no shader for that material/vertex combination.
    /// </summary>
    public abstract void DrawMesh(
        Renderer3D renderer,
        Mesh mesh,
        Material material,
        in Matrix4x4 transform);

    /// <summary>
    /// Instanced equivalent: draws <paramref name="mesh"/> once per
    /// entry in <paramref name="instances"/>, sharing the material
    /// across the batch. The subclass dispatches on <em>both</em>
    /// material kind and <typeparamref name="TInstance"/> to find a
    /// matching instanced shader; the per-instance struct layout is
    /// shader-coupled so not every (material, TInstance) pair is
    /// supported. Throws <see cref="MaterializerNotSupportedException"/>
    /// when no shader matches.
    /// </summary>
    /// <remarks>
    /// The instance span is consumed during the call; callers may reuse
    /// or discard the underlying buffer immediately after the call
    /// returns.
    /// </remarks>
    public abstract void DrawMesh<TInstance>(
        Renderer3D renderer,
        Mesh mesh,
        Material material,
        ReadOnlySpan<TInstance> instances)
        where TInstance : unmanaged;
}

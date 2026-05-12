using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Material-aware draw extensions on <see cref="Renderer3D"/>: pick a
/// shader from a <see cref="Material"/> via a <see cref="Materializer"/>
/// (defaulting to <see cref="StandardMaterializer.Default"/>) instead of
/// requiring the caller to name a shader. The shader-typed overloads on
/// <c>Renderer3D</c> itself remain the lower-level path; these are sugar
/// for the common "I have a mesh + material, just draw it" case and for
/// walking <see cref="Model"/>s.
/// </summary>
public static class Renderer3DDrawExtensions
{
    // ---- DrawMesh (mesh + material) ---------------------------------

    /// <summary>Identity transform.</summary>
    public static void DrawMesh(
        this Renderer3D renderer,
        Mesh mesh,
        Material material,
        Materializer? materializer = null)
        => (materializer ?? StandardMaterializer.Default)
            .DrawMesh(renderer, mesh, material, Matrix4x4.Identity);

    /// <summary>Draw <paramref name="mesh"/> with the shader picked for <paramref name="material"/>.</summary>
    public static void DrawMesh(
        this Renderer3D renderer,
        Mesh mesh,
        Material material,
        in Matrix4x4 transform,
        Materializer? materializer = null)
        => (materializer ?? StandardMaterializer.Default)
            .DrawMesh(renderer, mesh, material, transform);

    /// <summary>
    /// Instanced material draw: <paramref name="mesh"/> rendered once
    /// per entry in <paramref name="instances"/>.
    /// </summary>
    public static void DrawMesh<TInstance>(
        this Renderer3D renderer,
        Mesh mesh,
        Material material,
        ReadOnlySpan<TInstance> instances,
        Materializer? materializer = null)
        where TInstance : unmanaged
        => (materializer ?? StandardMaterializer.Default)
            .DrawMesh(renderer, mesh, material, instances);

    // ---- DrawMesh (mesh only -- uses Materializer.DefaultMaterial) --

    /// <summary>
    /// Draws <paramref name="mesh"/> with the materializer's
    /// <see cref="Materializer.DefaultMaterial"/> at identity.
    /// </summary>
    public static void DrawMesh(
        this Renderer3D renderer,
        Mesh mesh,
        Materializer? materializer = null)
    {
        var m = materializer ?? StandardMaterializer.Default;
        m.DrawMesh(renderer, mesh, m.DefaultMaterial, Matrix4x4.Identity);
    }

    /// <summary>
    /// Draws <paramref name="mesh"/> at <paramref name="transform"/>
    /// with the materializer's <see cref="Materializer.DefaultMaterial"/>.
    /// </summary>
    public static void DrawMesh(
        this Renderer3D renderer,
        Mesh mesh,
        in Matrix4x4 transform,
        Materializer? materializer = null)
    {
        var m = materializer ?? StandardMaterializer.Default;
        m.DrawMesh(renderer, mesh, m.DefaultMaterial, transform);
    }

    /// <summary>
    /// Instanced draw of <paramref name="mesh"/> with the
    /// materializer's <see cref="Materializer.DefaultMaterial"/>.
    /// </summary>
    public static void DrawMesh<TInstance>(
        this Renderer3D renderer,
        Mesh mesh,
        ReadOnlySpan<TInstance> instances,
        Materializer? materializer = null)
        where TInstance : unmanaged
    {
        var m = materializer ?? StandardMaterializer.Default;
        m.DrawMesh(renderer, mesh, m.DefaultMaterial, instances);
    }

    // ---- DrawModel ---------------------------------------------------

    /// <summary>Identity transform.</summary>
    public static void DrawModel(
        this Renderer3D renderer,
        Model model,
        Materializer? materializer = null)
        => DrawModel(renderer, model, Matrix4x4.Identity, materializer);

    /// <summary>Draw every submesh of <paramref name="model"/>.</summary>
    public static void DrawModel(
        this Renderer3D renderer,
        Model model,
        in Matrix4x4 transform,
        Materializer? materializer = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        var m = materializer ?? StandardMaterializer.Default;
        foreach (var submesh in model.Submeshes)
            m.DrawMesh(renderer, submesh.Mesh, submesh.Material, transform);
    }

    /// <summary>
    /// Instanced model draw: every submesh of <paramref name="model"/>
    /// is rendered once per entry in <paramref name="instances"/>.
    /// </summary>
    public static void DrawModel<TInstance>(
        this Renderer3D renderer,
        Model model,
        ReadOnlySpan<TInstance> instances,
        Materializer? materializer = null)
        where TInstance : unmanaged
    {
        ArgumentNullException.ThrowIfNull(model);
        var m = materializer ?? StandardMaterializer.Default;
        foreach (var submesh in model.Submeshes)
            m.DrawMesh(renderer, submesh.Mesh, submesh.Material, instances);
    }
}

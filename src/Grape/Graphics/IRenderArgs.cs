using System.Numerics;

namespace Grape;

/// <summary>
/// Opt-in interface that lets a per-draw arguments struct expose
/// <em>accessors</em> for fields the renderer can fill in from its own
/// state (camera view-projection, lights, etc.). Implementing structs
/// publish a getter/setter pair as <c>static</c> properties for each
/// field they care about; non-implemented fields default to
/// <c>null</c> and are simply ignored.
/// </summary>
/// <remarks>
/// <para>
/// The motivating problem: many shaders want the renderer's camera
/// composed into their per-draw transform, and many will want a
/// directional light, an ambient color, and so on. Encoding each
/// trait as a separate sub-class of <see cref="ShaderSet{TVertex,TArgs}"/>
/// would explode the type system (one subclass per trait combination)
/// and would force a new <c>DrawMesh</c> overload for every
/// combination.
/// </para>
/// <para>
/// Instead, every "renderer-injectable" trait becomes a
/// getter/setter pair on this single interface. An args struct
/// implements only the pairs it cares about; the rest stay
/// <c>null</c>. The renderer's
/// <see cref="Renderer3D.DrawSceneMesh{TVertex,TArgs}"/> overload
/// probes the non-null accessors at queue time and applies the
/// renderer's matching state.
/// </para>
/// <para>
/// The accessors are <see cref="Func{T,TResult}"/> properties rather
/// than <c>static abstract</c> methods so that opting *out* of a
/// trait is cost-free: leaving the property at its <c>null</c>
/// default just means "this struct doesn't have that field, skip
/// it." A <c>static abstract</c> shape would force every struct to
/// implement every member with a no-op, which is the boilerplate
/// this design exists to avoid.
/// </para>
/// <para>
/// New traits are added by appending more accessor pairs to this
/// interface (their default of <c>null</c> keeps existing structs
/// source-compatible) plus a matching apply step in
/// <see cref="Renderer3D.DrawSceneMesh{TVertex,TArgs}"/>.
/// </para>
/// </remarks>
/// <typeparam name="TSelf">
/// The implementing struct itself, threaded through so the
/// <c>SetX</c> accessors return the correct concrete type.
/// </typeparam>
public interface IRenderArgs<TSelf>
    where TSelf : unmanaged, IRenderArgs<TSelf>
{
    /// <summary>
    /// Reads the model-view-projection (or model-only) transform out
    /// of the args struct. <c>null</c> means the struct has no
    /// transform field; the renderer will skip composing the camera
    /// into it.
    /// </summary>
    static virtual Func<TSelf, Matrix4x4>? GetTransform { get; } = null;

    /// <summary>
    /// Returns a copy of the args struct with the given transform
    /// installed. Paired with <see cref="GetTransform"/>; both must
    /// be non-null for the renderer to apply the camera.
    /// </summary>
    static virtual Func<TSelf, Matrix4x4, TSelf>? SetTransform { get; } = null;
}

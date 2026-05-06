using System.Numerics;

namespace Grape;

/// <summary>
/// A renderer that renders 3D graphics to a target.
/// </summary>
public abstract class Renderer3D
{
    // Snapshots pushed by PushState() and popped on scope dispose. The
    // stack is allocated once per renderer and grown lazily, so push/pop
    // is allocation-free in steady state.
    private readonly Stack<RendererState> _stateStack = new();

    /// <summary>
    /// How the next draw interacts with the depth buffer. Snapshotted
    /// into each <c>RenderMesh</c> call at the time of the call, so
    /// changing this value after a draw is queued has no effect on it.
    /// </summary>
    public DepthMode DepthMode { get; set; } = DepthMode.Solid;

    /// <summary>
    /// Which triangles to skip based on facing direction. Defaults to
    /// <see cref="CullMode.None"/> so hand-built or single-sided geometry
    /// just works; switch to <see cref="CullMode.Back"/> for closed solid
    /// meshes to halve their fragment work. Snapshotted per draw, like
    /// <see cref="DepthMode"/>.
    /// </summary>
    public CullMode CullMode { get; set; } = CullMode.None;

    /// <summary>
    /// When true, triangle-based meshes draw as their unique edges
    /// instead of filled triangles. The renderer derives a deduped edge
    /// index buffer from the mesh's triangles once per change and
    /// caches it. Non-triangle meshes (lines, points) draw normally.
    /// Snapshotted per draw, like <see cref="DepthMode"/>.
    /// </summary>
    public bool Wireframe { get; set; } = false;

    /// <summary>
    /// Saves the current renderer state and returns a scope whose
    /// disposal restores it. Intended for use with a <c>using</c>
    /// statement so callers can change state for a sub-region of drawing
    /// without having to remember and reset every property by hand.
    /// </summary>
    public StateScope PushState()
    {
        _stateStack.Push(new RendererState(DepthMode, CullMode, Wireframe));
        return new StateScope(this);
    }

    private void PopState()
    {
        var s = _stateStack.Pop();
        DepthMode = s.DepthMode;
        CullMode = s.CullMode;
        Wireframe = s.Wireframe;
    }

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

    // Snapshot of every property a PushState/PopState cycle has to save
    // and restore. Add a field here whenever a new mutable knob is added
    // to Renderer3D so existing callers that already use PushState don't
    // have to change.
    private readonly record struct RendererState(DepthMode DepthMode, CullMode CullMode, bool Wireframe);

    /// <summary>
    /// A disposable scope returned from <see cref="PushState"/>. Disposing
    /// it restores the renderer state captured at the matching push. As a
    /// <c>ref struct</c> it cannot be stored in fields, captured by
    /// closures, or moved across <c>await</c> boundaries -- the only valid
    /// use is in a <c>using</c> statement on the same call frame as the
    /// push.
    /// </summary>
    public ref struct StateScope
    {
        private Renderer3D? _renderer;

        internal StateScope(Renderer3D renderer)
        {
            _renderer = renderer;
        }

        public void Dispose()
        {
            var r = _renderer;
            _renderer = null;
            r?.PopState();
        }
    }
}

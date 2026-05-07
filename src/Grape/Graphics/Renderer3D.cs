using System.Diagnostics;
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

    private readonly long _startTs = Stopwatch.GetTimestamp();
    private long _lastRenderTs = Stopwatch.GetTimestamp();

    /// <summary>
    /// Elapsed wall-clock time since this renderer was created. Useful as
    /// an animation phase (e.g. <c>sin(t)</c>) that keeps advancing through
    /// minimisation, debugger breaks, and system sleep.
    /// </summary>
    public TimeSpan ElapsedSinceStart => Stopwatch.GetElapsedTime(_startTs);

    /// <summary>
    /// Elapsed wall-clock time since the last call to <see cref="GpuRenderer.Render"/>
    /// (or since renderer creation if no frame has been rendered yet),
    /// clamped by <see cref="MaxFrameDelta"/> so a long pause doesn't
    /// teleport time-integrated state.
    /// </summary>
    public TimeSpan ElapsedSinceLastRender
    {
        get
        {
            var elapsed = Stopwatch.GetElapsedTime(_lastRenderTs);
            return elapsed > MaxFrameDelta ? MaxFrameDelta : elapsed;
        }
    }

    /// <summary>
    /// Upper bound on <see cref="ElapsedSinceLastRender"/>. Long pauses
    /// (window minimised, debugger break, system sleep) would otherwise
    /// produce a single huge delta that teleports time-integrated state.
    /// Set to <see cref="TimeSpan.MaxValue"/> to disable clamping.
    /// </summary>
    public TimeSpan MaxFrameDelta { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Color used to clear the render target at the start of each frame
    /// when <see cref="AutoClear"/> is true. Set by the owning window;
    /// not user-mutable through the renderer.
    /// </summary>
    public Color BackgroundColor { get; internal set; }

    /// <summary>
    /// How frames are scheduled against the display's vertical blank.
    /// Treated as a hint: unsupported modes fall back to the next-best
    /// supported mode. Defaults to <see cref="SyncMode.WaitForSync"/>.
    /// </summary>
    public virtual SyncMode SyncMode { get; set; } = SyncMode.WaitForSync;

    /// <summary>
    /// When true (the default), each frame begins by clearing the render
    /// target to <see cref="BackgroundColor"/>. Set to false for additive
    /// or persistence-of-pixels rendering.
    /// </summary>
    internal bool AutoClear { get; set; } = true;

    /// <summary>
    /// Resets the <see cref="ElapsedSinceLastRender"/> clock. Concrete
    /// renderers call this from their <c>Render()</c> implementation
    /// after the frame has been submitted.
    /// </summary>
    private protected void AdvanceFrameClock()
    {
        _lastRenderTs = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// How the next draw interacts with the depth buffer. Snapshotted
    /// into each <c>DrawMesh</c> call at the time of the call, so
    /// changing this value after a draw is queued has no effect on it.
    /// </summary>
    public DepthMode DepthMode { get; set; } = DepthMode.Solid;

    /// <summary>
    /// How the next draw's output color combines with the existing
    /// pixels in the render target. Defaults to <see cref="BlendMode.Alpha"/>.
    /// Snapshotted per draw, like <see cref="DepthMode"/>.
    /// </summary>
    /// <remarks>
    /// Translucent modes are order-dependent: draw far things first.
    /// Pair translucent draws with <see cref="DepthMode.Transparent"/>
    /// so they don't write depth and occlude things behind them.
    /// </remarks>
    public BlendMode BlendMode { get; set; } = BlendMode.Alpha;

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
    /// Rectangle of the render target the scene is mapped into. The
    /// vertex shader's clip-space output is scaled to fit this region.
    /// Coordinates are in pixels of the render target, with (0, 0) at
    /// the top-left. <see langword="null"/> (the default) means the
    /// full render target.
    /// </summary>
    /// <remarks>
    /// A half-width viewport squishes the entire scene horizontally
    /// into half the screen -- this is the property to use for
    /// split-screen, picture-in-picture, or rendering a scene into a
    /// HUD panel. To restrict <em>which pixels</em> can be touched
    /// without scaling the scene, use <see cref="ClipRect"/> instead.
    /// Snapshotted per draw, like <see cref="DepthMode"/>.
    /// </remarks>
    public Rect? Viewport { get; set; } = null;

    /// <summary>
    /// Rectangle outside which fragment writes are discarded. Unlike
    /// <see cref="Viewport"/>, this does not scale the scene -- it just
    /// masks pixels. Coordinates are in pixels of the render target,
    /// with (0, 0) at the top-left. <see langword="null"/> (the
    /// default) means the full render target.
    /// </summary>
    /// <remarks>
    /// Maps to GPU scissor under the hood. Use this to clip to a UI
    /// panel, mask a sub-region, or skip fragments you know will be
    /// hidden. Snapshotted per draw, like <see cref="DepthMode"/>.
    /// </remarks>
    public Rect? ClipRect { get; set; } = null;

    /// <summary>
    /// Anti-aliasing level for triangle silhouettes. Latched at the
    /// start of each frame's render pass; changing it between
    /// <c>DrawMesh</c> calls within one frame has no effect on that
    /// frame -- the new value applies to the next frame.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="Antialiasing.None"/>. <see cref="Antialiasing.X4"/>
    /// is a good baseline upgrade. Memory and fill-rate cost scale
    /// linearly with the sample count; fragment shading cost stays the
    /// same (one shader invocation per pixel, regardless of MSAA level).
    /// MSAA does not help with texture aliasing inside surfaces — use
    /// mipmaps (<see cref="Image.Mipmaps"/>) for that.
    /// </remarks>
    public Antialiasing Antialiasing { get; set; } = Antialiasing.None;

    /// <summary>
    /// Saves the current renderer state and returns a scope whose
    /// disposal restores it. Intended for use with a <c>using</c>
    /// statement so callers can change state for a sub-region of drawing
    /// without having to remember and reset every property by hand.
    /// </summary>
    public StateScope PushState()
    {
        _stateStack.Push(new RendererState(DepthMode, BlendMode, CullMode, Wireframe, Viewport, ClipRect));
        return new StateScope(this);
    }

    private void PopState()
    {
        var s = _stateStack.Pop();
        DepthMode = s.DepthMode;
        BlendMode = s.BlendMode;
        CullMode = s.CullMode;
        Wireframe = s.Wireframe;
        Viewport = s.Viewport;
        ClipRect = s.ClipRect;
    }

    /// <summary>
    /// Queues a mesh for drawing using a compatible <see cref="ShaderSet{TVertex}"/>.
    /// </summary>
    public abstract void DrawMesh<TVertex>(Mesh<TVertex> mesh, ShaderSet<TVertex> shader)
        where TVertex : unmanaged;

    /// <summary>
    /// Queues a mesh for drawing using a compatible <see cref="ShaderSet{TVertex,TArgs}"/> with the given per-draw arguments.
    /// </summary>
    public abstract void DrawMesh<TVertex, TArgs>(
        Mesh<TVertex> mesh,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args)
        where TVertex : unmanaged
        where TArgs : unmanaged;

    /// <summary>Queues a mesh for drawing, sampling from the given image.</summary>
    public abstract void DrawMesh<TVertex>(
        Mesh<TVertex> mesh,
        Image texture,
        ShaderSet<TVertex> shader)
        where TVertex : unmanaged;

    /// <summary>Queues a textured mesh for drawing using a shader with typed per-draw args.</summary>
    public abstract void DrawMesh<TVertex, TArgs>(
        Mesh<TVertex> mesh,
        Image texture,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args)
        where TVertex : unmanaged
        where TArgs : unmanaged;

    /// <summary>
    /// Queues a mesh for instanced drawing. The mesh is drawn once per
    /// entry in <paramref name="instances"/>; each instance picks up its
    /// own per-instance values from the matching slot, and all instances
    /// share the per-call <paramref name="args"/>. The instance span is
    /// only read for the duration of the call -- callers may reuse or
    /// discard the underlying buffer immediately after the call returns.
    /// </summary>
    public abstract void DrawMesh<TVertex, TArgs, TInstance>(
        Mesh<TVertex> mesh,
        InstancedShaderSet<TVertex, TArgs, TInstance> shader,
        in TArgs args,
        ReadOnlySpan<TInstance> instances)
        where TVertex : unmanaged
        where TArgs : unmanaged
        where TInstance : unmanaged;

    /// <summary>
    /// Textured variant of the instanced draw overload. Same per-instance
    /// semantics; <paramref name="texture"/> is bound to fragment sampler
    /// slot 0 for the whole batch.
    /// </summary>
    public abstract void DrawMesh<TVertex, TArgs, TInstance>(
        Mesh<TVertex> mesh,
        Image texture,
        InstancedShaderSet<TVertex, TArgs, TInstance> shader,
        in TArgs args,
        ReadOnlySpan<TInstance> instances)
        where TVertex : unmanaged
        where TArgs : unmanaged
        where TInstance : unmanaged;

    /// <summary>Queues ASCII debug text for drawing at the given world-space transform.</summary>
    public abstract void DrawDebugText(string text, in Matrix4x4 transform);

    /// <summary>
    /// When true, calls to <see cref="Render"/> become no-ops. Used by
    /// <see cref="Window3D"/> to suppress stray <c>Render()</c> calls
    /// from inside a <c>Rendering</c> event handler so the window itself
    /// can own the single per-event flush.
    /// </summary>
    internal bool RenderSuppressed { get; set; }

    /// <summary>
    /// Renders the entire frame to the output target.
    /// Call this to manually render at any time. 
    /// This is unnecessary when rendering within Rendering event handlers.
    /// </summary>
    public void Render()
    {
        if (RenderSuppressed)
            return;
        // Marshal to the application thread so callers can invoke Render()
        // from any thread; Send is a no-op when already on the app thread.
        Application.Current.Send(_ => RenderOnApplicationThread());
    }

    /// <summary>
    /// Performs the actual frame rendering. Always invoked on the
    /// application thread by <see cref="Render"/>.
    /// </summary>
    protected abstract void RenderOnApplicationThread();

    // Snapshot of every property a PushState/PopState cycle has to save
    // and restore. Add a field here whenever a new mutable knob is added
    // to Renderer3D so existing callers that already use PushState don't
    // have to change.
    private readonly record struct RendererState(
        DepthMode DepthMode,
        BlendMode BlendMode,
        CullMode CullMode,
        bool Wireframe,
        Rect? Viewport,
        Rect? ClipRect);

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

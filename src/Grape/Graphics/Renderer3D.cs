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
    /// The camera used to compose view-projection matrices for
    /// <see cref="DrawSceneMesh{TVertex,TArgs}(Mesh{TVertex},
    /// ShaderSet{TVertex,TArgs}, in TArgs)"/>. <see langword="null"/>
    /// (the default) means scene-aware draws skip applying any view-
    /// projection transform -- the args struct's transform field, if
    /// any, is sent to the GPU unchanged.
    /// </summary>
    public Camera3D? Camera { get; set; }

    /// <summary>
    /// Whole-scene ambient light, applied by lit shaders that opt in via
    /// <see cref="IRenderArgs{TSelf}.SetAmbientLight"/>. The RGB channels
    /// are added unconditionally to every lit fragment; the default of
    /// <see cref="Color.Black"/> contributes nothing, so unlit shaders and
    /// scenes that just want a directional light see no change.
    /// </summary>
    public Color AmbientLight { get; set; } = Color.Black;

    /// <summary>
    /// Optional infinitely-distant light. When non-null and the args
    /// struct opts in via <see cref="IRenderArgs{TSelf}.SetDirectionalLight"/>,
    /// lit shaders combine its contribution with <see cref="AmbientLight"/>
    /// using a Lambertian (N·L) term. <c>null</c> (the default) skips
    /// the directional contribution entirely; lit surfaces fall back to
    /// just the ambient term.
    /// </summary>
    public DirectionalLight? DirectionalLight { get; set; }

    /// <summary>
    /// Mutable list of point lights that lit shaders accumulate per
    /// fragment. Add/remove freely between frames; the renderer
    /// snapshots and uploads the list at the start of each
    /// <c>Render()</c>. There is no fixed cap -- the storage buffer
    /// grows on demand.
    /// </summary>
    /// <remarks>
    /// Mutating the list <em>between</em> queued draws within a single
    /// frame works but is unusual: the per-draw count uniform is taken
    /// at the time <c>DrawMesh</c> is called, while the buffer contents
    /// are taken at render time, so adding lights between draws affects
    /// only later draws in the same frame and removing lights between
    /// draws may leave earlier draws looping over slots that no longer
    /// describe a valid light. Stick to the "mutate between frames"
    /// pattern unless you have a specific reason.
    /// </remarks>
    public List<PointLight> PointLights { get; } = new();

    /// <summary>
    /// Aspect ratio (width / height) used when sampling the camera's
    /// projection in scene-aware draws. Computed from the active
    /// render target's pixel dimensions; concrete renderers override
    /// <see cref="GetTargetAspectRatio"/> to supply the real value.
    /// Falls back to 16:9 when no target dimensions are known yet.
    /// </summary>
    public float AspectRatio => GetTargetAspectRatio();

    /// <summary>
    /// Override in concrete renderers to expose the current render
    /// target's aspect ratio. Called from
    /// <see cref="AspectRatio"/> on every read so resize is picked up
    /// automatically.
    /// </summary>
    protected virtual float GetTargetAspectRatio() => 16f / 9f;

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
    /// Queues a mesh for drawing using a compatible <see cref="ShaderSet{TVertex,TArgs}"/>
    /// with the given per-draw arguments. The args struct is forwarded to the
    /// shader unchanged -- no renderer state (camera, lights, ...) is composed
    /// into it. This is the escape hatch for shaders whose arg layout doesn't
    /// fit <see cref="IRenderArgs{TSelf}"/>; prefer
    /// <see cref="DrawMesh{TVertex,TArgs}(Mesh{TVertex}, ShaderSet{TVertex,TArgs}, in TArgs)"/>
    /// when your args struct can opt in.
    /// </summary>
    public abstract void DrawMeshRaw<TVertex, TArgs>(
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

    /// <summary>Raw textured draw with no scene composition. See
    /// <see cref="DrawMeshRaw{TVertex,TArgs}(Mesh{TVertex}, ShaderSet{TVertex,TArgs}, in TArgs)"/>.</summary>
    public abstract void DrawMeshRaw<TVertex, TArgs>(
        Mesh<TVertex> mesh,
        Image texture,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args)
        where TVertex : unmanaged
        where TArgs : unmanaged;

    /// <summary>
    /// Cubemap variant of the textured draw overload. The shader's
    /// fragment-stage texture binding (slot 0) must declare a
    /// <c>TextureCube</c> rather than a <c>Texture2D</c>; pair this
    /// with <see cref="Shaders.Skybox"/> or another cubemap shader.
    /// </summary>
    public abstract void DrawMesh<TVertex>(
        Mesh<TVertex> mesh,
        Cubemap cubemap,
        ShaderSet<TVertex> shader)
        where TVertex : unmanaged;

    /// <summary>
    /// Raw cubemap draw with no scene composition. See
    /// <see cref="DrawMeshRaw{TVertex,TArgs}(Mesh{TVertex}, ShaderSet{TVertex,TArgs}, in TArgs)"/>.
    /// Used by <see cref="Shaders.Skybox"/>, which needs a translation-stripped
    /// view-projection that the regular camera composition can't supply.
    /// </summary>
    public abstract void DrawMeshRaw<TVertex, TArgs>(
        Mesh<TVertex> mesh,
        Cubemap cubemap,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args)
        where TVertex : unmanaged
        where TArgs : unmanaged;

    /// <summary>
    /// Queues a mesh for drawing using a compatible <see cref="ShaderSet{TVertex,TArgs}"/>
    /// with the given per-draw arguments, composing the renderer's
    /// <see cref="Camera"/> view-projection (and, in the future, lights,
    /// ambient, time, ...) into the args struct via
    /// <see cref="IRenderArgs{TSelf}"/> accessors before forwarding to the
    /// underlying draw path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pass a model matrix (or any args struct holding one); the shader will
    /// receive <c>model * camera.GetViewProjection(aspect)</c>. When
    /// <see cref="Camera"/> is <see langword="null"/> the args struct is
    /// forwarded unchanged.
    /// </para>
    /// <para>
    /// New traits (lights, ambient, etc.) appear as additional accessor pairs
    /// on <see cref="IRenderArgs{TSelf}"/> with corresponding apply steps in
    /// this overload. Args structs that don't opt in to a given trait are
    /// unaffected.
    /// </para>
    /// <para>
    /// For args layouts that <em>can't</em> implement <see cref="IRenderArgs{TSelf}"/>
    /// (e.g. a bare <see cref="System.Numerics.Matrix4x4"/>), use
    /// <see cref="DrawMeshRaw{TVertex,TArgs}(Mesh{TVertex}, ShaderSet{TVertex,TArgs}, in TArgs)"/>.
    /// </para>
    /// </remarks>
    public void DrawMesh<TVertex, TArgs>(
        Mesh<TVertex> mesh,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args)
        where TVertex : unmanaged
        where TArgs : unmanaged, IRenderArgs<TArgs>
    {
        var resolved = ApplyRenderArgs(args);
        DrawMeshRaw(mesh, shader, in resolved);
    }

    /// <summary>
    /// Scene-aware textured draw. See
    /// <see cref="DrawMesh{TVertex,TArgs}(Mesh{TVertex}, ShaderSet{TVertex,TArgs}, in TArgs)"/>
    /// for trait-application behavior.
    /// </summary>
    public void DrawMesh<TVertex, TArgs>(
        Mesh<TVertex> mesh,
        Image texture,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args)
        where TVertex : unmanaged
        where TArgs : unmanaged, IRenderArgs<TArgs>
    {
        var resolved = ApplyRenderArgs(args);
        DrawMeshRaw(mesh, texture, shader, in resolved);
    }

    /// <summary>
    /// Scene-aware cubemap draw. See
    /// <see cref="DrawMesh{TVertex,TArgs}(Mesh{TVertex}, ShaderSet{TVertex,TArgs}, in TArgs)"/>
    /// for trait-application behavior.
    /// </summary>
    public void DrawMesh<TVertex, TArgs>(
        Mesh<TVertex> mesh,
        Cubemap cubemap,
        ShaderSet<TVertex, TArgs> shader,
        in TArgs args)
        where TVertex : unmanaged
        where TArgs : unmanaged, IRenderArgs<TArgs>
    {
        var resolved = ApplyRenderArgs(args);
        DrawMeshRaw(mesh, cubemap, shader, in resolved);
    }

    // The single point where renderer state is pushed into a special
    // args struct. New traits land here as additional non-null-accessor
    // checks; an args struct that doesn't expose a given accessor is
    // unaffected.
    private TArgs ApplyRenderArgs<TArgs>(TArgs args)
        where TArgs : unmanaged, IRenderArgs<TArgs>
    {
        // Camera -> single MVP transform field (compose model * view-projection).
        // Used by shaders that pack model and view-projection into one matrix.
        if (Camera is { } camera
            && TArgs.GetTransform is { } get
            && TArgs.SetTransform is { } set)
        {
            var model = get(args);
            var vp = camera.GetViewProjection(AspectRatio);
            args = set(args, model * vp);
        }

        // Camera -> separate view-projection field. Used by shaders that
        // keep the model matrix separate from view-projection (e.g. lit
        // shaders that need the unmultiplied model matrix to transform
        // normals).
        if (Camera is { } cam2
            && TArgs.SetViewProjection is { } setVp)
        {
            args = setVp(args, cam2.GetViewProjection(AspectRatio));
        }

        // Ambient light -> ambient field. Always fires when the args
        // struct opts in; default ambient is Color.Black, which adds no
        // visible contribution.
        if (TArgs.SetAmbientLight is { } setAmb)
        {
            args = setAmb(args, AmbientLight);
        }

        // Directional light -> light field. Only fires when a light is
        // configured; otherwise the args struct keeps whatever the user
        // initialised (typically zero, which the shader treats as off).
        if (DirectionalLight is { } dirLight
            && TArgs.SetDirectionalLight is { } setDir)
        {
            args = setDir(args, dirLight);
        }

        // Point light count -> count field. Always fires when the args
        // struct opts in; the storage buffer itself is bound by the
        // concrete renderer at draw time. Count of zero is a no-op in
        // the shader's loop.
        if (TArgs.SetPointLightCount is { } setCount)
        {
            args = setCount(args, PointLights.Count);
        }

        return args;
    }

    /// <summary>
    /// Queues a mesh for instanced drawing. The mesh is drawn once per
    /// entry in <paramref name="instances"/>; each instance picks up its
    /// own per-instance values from the matching slot, and all instances
    /// share the per-call <paramref name="args"/>. The instance span is
    /// only read for the duration of the call -- callers may reuse or
    /// discard the underlying buffer immediately after the call returns.
    /// </summary>
    /// <remarks>
    /// Instanced draws skip scene composition: callers must supply the full
    /// view-projection in <paramref name="args"/> themselves. The
    /// <c>Raw</c> suffix flags this -- there is no scene-aware instanced
    /// overload yet because the per-call args for instanced shaders is
    /// almost always exactly the camera's view-projection (no model matrix
    /// to compose with), so the convenience would be small.
    /// </remarks>
    public abstract void DrawMeshRaw<TVertex, TArgs, TInstance>(
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
    public abstract void DrawMeshRaw<TVertex, TArgs, TInstance>(
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

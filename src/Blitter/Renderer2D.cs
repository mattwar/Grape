using System.Diagnostics;
using System.Numerics;

namespace Blitter;

/// <summary>
/// A renderer that renders 2D graphics to a target.
/// </summary>
public abstract class Renderer2D
{
    private readonly long _startTs = Stopwatch.GetTimestamp();
    private long _lastRenderTs = Stopwatch.GetTimestamp();

    /// <summary>
    /// Elapsed wall-clock time since this renderer was created.
    /// </summary>
    public TimeSpan ElapsedSinceStart => Stopwatch.GetElapsedTime(_startTs);

    /// <summary>
    /// Elapsed wall-clock time since the last call to <c>Render()</c>
    /// (or since renderer creation if no frame has been rendered yet),
    /// clamped by <see cref="MaxFrameDelta"/>.
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
    /// <see cref="ElapsedSinceStart"/> as <c>float</c> seconds. Convenient
    /// for shader uniforms and animation phase math.
    /// </summary>
    public float ElapsedSecondsSinceStart =>
        (float)ElapsedSinceStart.TotalSeconds;

    /// <summary>
    /// <see cref="ElapsedSinceLastRender"/> as <c>float</c> seconds.
    /// Convenient as a per-frame <c>dt</c> for time-integrated state.
    /// </summary>
    public float ElapsedSecondsSinceLastRender =>
        (float)ElapsedSinceLastRender.TotalSeconds;

    /// <summary>
    /// Upper bound on <see cref="ElapsedSinceLastRender"/>. Set to
    /// <see cref="TimeSpan.MaxValue"/> to disable clamping.
    /// </summary>
    public TimeSpan MaxFrameDelta { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Color used to clear the render target before the first draw of
    /// each frame when <see cref="AutoClear"/> is true. Set by the
    /// owning window; not user-mutable through the renderer.
    /// </summary>
    public Color BackgroundColor { get; internal set; }

    /// <summary>
    /// How frames are scheduled against the display's vertical blank.
    /// Treated as a hint: unsupported modes fall back to the next-best
    /// supported mode. Defaults to <see cref="SyncMode.WaitForSync"/>.
    /// </summary>
    public virtual SyncMode SyncMode { get; set; } = SyncMode.WaitForSync;

    /// <summary>
    /// When true (the default), the renderer clears the target to
    /// <see cref="BackgroundColor"/> before the first draw of each
    /// frame. Set to false for additive or persistence-of-pixels
    /// rendering.
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
    /// Builds a per-frame <see cref="UpdateContext2D"/> snapshotting this
    /// renderer's clock and target bounds. Convenience for the common
    /// case where one loop drives both update and render; standalone
    /// simulations should build their own context from their own clock.
    /// </summary>
    public UpdateContext2D GetUpdateContext()
    {
        var (w, h) = OutputSize;
        return new UpdateContext2D
        {
            ElapsedSinceStart = ElapsedSinceStart,
            ElapsedSinceLastUpdate = ElapsedSinceLastRender,
            Bounds = new Rect(0, 0, w, h),
        };
    }

    #region State

    /// <summary>Clipping rectangle for subsequent draws.</summary>
    public abstract Rect ClipRect { get; set; }

    /// <summary>Per-channel color scale applied to draw colors.</summary>
    public abstract float ColorScale { get; set; }

    /// <summary>The current draw color.</summary>
    public abstract Color DrawColor { get; set; }

    /// <summary>Logical presentation rectangle for the current presentation mode.</summary>
    public abstract Rect LogicalRepresentationRect { get; }

    /// <summary>The output size in pixels.</summary>
    public abstract (int Width, int Height) OutputSize { get; }

    /// <summary>
    /// Output width / height as a single <c>float</c>. Reads
    /// <see cref="OutputSize"/> on every call so resizing is picked up
    /// automatically.
    /// </summary>
    public float AspectRatio
    {
        get
        {
            var (w, h) = OutputSize;
            return h == 0 ? 0f : (float)w / h;
        }
    }

    /// <summary>The rendering scale factors.</summary>
    public abstract (float ScaleX, float ScaleY) Scale { get; set; }

    /// <summary>The portion of the rendering target where draws are performed.</summary>
    public abstract Rect ViewPort { get; set; }

    /// <summary>
    /// Configures a fixed logical drawing surface that the renderer
    /// scales to fit the actual output. After calling this, all draws
    /// use coordinates in the (<paramref name="width"/>,
    /// <paramref name="height"/>) space and the renderer handles
    /// scaling, centering, and letterbox bars automatically.
    /// Pass <see cref="LogicalPresentation.Disabled"/> with any size
    /// to turn it off.
    /// </summary>
    public abstract void SetLogicalSize(int width, int height, LogicalPresentation mode);

    #endregion

    #region Frame

    /// <summary>Fills the current draw target with <see cref="DrawColor"/>.</summary>
    public abstract void Clear();

    /// <summary>
    /// When true, calls to <see cref="Render"/> become no-ops. Used by
    /// <see cref="Window2D"/> to suppress stray <c>Render()</c> calls
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

    #endregion

    #region Drawing

    /// <summary>Draws debug text at the given location.</summary>
    public abstract bool DrawDebugText(int x, int y, string text, float scale = 0f);

    /// <summary>Draws a portion of <paramref name="image"/> to a destination rectangle.</summary>
    public abstract bool DrawImage(Image image, Rect source, Rect destination);

    /// <summary>Draws the entire <paramref name="image"/> to a destination rectangle.</summary>
    public bool DrawImage(Image image, Rect destination)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (w, h) = image.Size;
        return DrawImage(image, new Rect(0, 0, w, h), destination);
    }

    /// <summary>Draws the entire <paramref name="image"/> at a position with optional uniform scale.</summary>
    public bool DrawImage(Image image, float x, float y, float scale = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (w, h) = image.Size;
        var source = new Rect(0, 0, w, h);
        var destination = new Rect(x, y, w * scale, h * scale);
        return DrawImage(image, source, destination);
    }

    /// <summary>Draws a portion of <paramref name="image"/> rotated about <paramref name="center"/>.</summary>
    public abstract bool DrawImageRotated(Image image, Rect source, Rect destination, float angle, Vector2 center, FlipMode flip = FlipMode.None);

    /// <summary>Draws the entire <paramref name="image"/> rotated about <paramref name="center"/>.</summary>
    public bool DrawImageRotated(Image image, Rect destination, float angle, Vector2 center, FlipMode flip = FlipMode.None)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (w, h) = image.Size;
        return DrawImageRotated(image, new Rect(0, 0, w, h), destination, angle, center, flip);
    }

    /// <summary>Draws the entire <paramref name="image"/> at a position rotated about a center.</summary>
    public bool DrawImageRotated(Image image, float x, float y, float angle, float centerX, float centerY, float scale = 1.0f, FlipMode flip = FlipMode.None)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (w, h) = image.Size;
        var source = new Rect(0, 0, w, h);
        var destination = new Rect(x, y, w * scale, h * scale);
        var center = new Vector2(centerX * scale, centerY * scale);
        return DrawImageRotated(image, source, destination, angle, center, flip);
    }

    /// <summary>Fills <paramref name="rect"/> with <see cref="DrawColor"/>.</summary>
    public abstract bool DrawFillRect(Rect rect);

    /// <summary>Fills each rectangle in <paramref name="rects"/> with <see cref="DrawColor"/>.</summary>
    public abstract bool DrawFillRects(ReadOnlySpan<Rect> rects);

    /// <summary>
    /// Draws an indexed triangle list, optionally sampling from
    /// <paramref name="image"/>.
    /// </summary>
    public abstract bool DrawGeometry(ReadOnlySpan<Vertex2D> vertices, ReadOnlySpan<int> indices, Image? image = null);

    /// <summary>Draws a line between two points.</summary>
    public abstract bool DrawLine(float x1, float y1, float x2, float y2);

    /// <summary>Draws a connected polyline through <paramref name="points"/>.</summary>
    public abstract bool DrawLines(ReadOnlySpan<Vector2> points);

    /// <summary>Draws a single point.</summary>
    public abstract bool DrawPoint(float x, float y);

    /// <summary>Draws a set of points.</summary>
    public abstract bool DrawPoints(ReadOnlySpan<Vector2> points);

    #endregion
}

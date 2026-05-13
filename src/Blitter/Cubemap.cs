namespace Blitter;

/// <summary>
/// A cubemap is six square images arranged as the inside of a cube,
/// sampled by a 3D direction vector (rather than a 2D UV). Used for
/// skyboxes, environment reflections, and image-based lighting.
/// </summary>
/// <remarks>
/// <para>
/// Each face represents the world-space view through one wall of a
/// 1×1×1 cube centered at the origin. The face name says which
/// world-space axis the face is the +x/-x/+y/-y/+z/-z wall of.
/// All six faces must share the same square pixel dimensions and the
/// same <see cref="Blitter.PixelFormat"/>.
/// </para>
/// <para>
/// Face image orientation follows the Direct3D / Vulkan / Metal
/// convention: pixel (0, 0) of each face image is the upper-left
/// corner as seen from the cube's center looking outward through that
/// face. If your source images come from a pack with a different
/// orientation, use <see cref="BitmapImage.Flip"/> and
/// <see cref="BitmapImage.Rotate"/> on each face before passing it in.
/// </para>
/// <para>
/// Like <see cref="Image"/>, a <c>Cubemap</c> is a CPU-side handle.
/// Renderers upload it to the GPU lazily and cache the result, keyed
/// on <see cref="Version"/>. Bumping <see cref="Version"/> -- by
/// reassigning a face image's pixels and calling
/// <see cref="Invalidate"/> -- forces the next draw to re-upload all
/// six faces.
/// </para>
/// </remarks>
public sealed class Cubemap
{
    private int _version;

    private Cubemap(
        MipmappedImage positiveX, MipmappedImage negativeX,
        MipmappedImage positiveY, MipmappedImage negativeY,
        MipmappedImage positiveZ, MipmappedImage negativeZ,
        bool mipmaps)
    {
        PositiveXLevels = positiveX;
        NegativeXLevels = negativeX;
        PositiveYLevels = positiveY;
        NegativeYLevels = negativeY;
        PositiveZLevels = positiveZ;
        NegativeZLevels = negativeZ;
        Mipmaps = mipmaps;
        _version = 1;

        Size = positiveX.Width;
        Format = positiveX.PixelFormat;
        LevelCount = positiveX.LevelCount;
    }

    /// <summary>The +X face: looking toward world +X (right).</summary>
    public Image PositiveX => PositiveXLevels.Base;
    /// <summary>The -X face: looking toward world -X (left).</summary>
    public Image NegativeX => NegativeXLevels.Base;
    /// <summary>The +Y face: looking toward world +Y (up).</summary>
    public Image PositiveY => PositiveYLevels.Base;
    /// <summary>The -Y face: looking toward world -Y (down).</summary>
    public Image NegativeY => NegativeYLevels.Base;
    /// <summary>The +Z face: looking toward world +Z (forward in a left-handed system, backward in a right-handed system).</summary>
    public Image PositiveZ => PositiveZLevels.Base;
    /// <summary>The -Z face: looking toward world -Z.</summary>
    public Image NegativeZ => NegativeZLevels.Base;

    /// <summary>The +X face's mip chain.</summary>
    public MipmappedImage PositiveXLevels { get; }
    /// <summary>The -X face's mip chain.</summary>
    public MipmappedImage NegativeXLevels { get; }
    /// <summary>The +Y face's mip chain.</summary>
    public MipmappedImage PositiveYLevels { get; }
    /// <summary>The -Y face's mip chain.</summary>
    public MipmappedImage NegativeYLevels { get; }
    /// <summary>The +Z face's mip chain.</summary>
    public MipmappedImage PositiveZLevels { get; }
    /// <summary>The -Z face's mip chain.</summary>
    public MipmappedImage NegativeZLevels { get; }

    /// <summary>
    /// Edge length of every face's base mip level, in pixels. All six
    /// faces are square and share this size.
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// Pixel format shared by all six faces.
    /// </summary>
    public PixelFormat Format { get; }

    /// <summary>
    /// Number of mip levels supplied per face. <c>1</c> means only a
    /// base level was provided. When <c>&gt; 1</c>, every face's
    /// <see cref="MipmappedImage.LevelCount"/> is this same value.
    /// </summary>
    public int LevelCount { get; }

    /// <summary>
    /// When <c>true</c>, hints to renderers to generate a full mipmap
    /// chain for the cubemap. Required for image-based lighting and
    /// helps reduce shimmer when sampled at oblique angles.
    /// </summary>
    public bool Mipmaps { get; }

    /// <summary>
    /// Bumped each time the cubemap's contents change. Renderers use
    /// this to detect when their cached GPU upload is stale.
    /// </summary>
    public int Version => _version;

    /// <summary>
    /// Marks the cubemap contents as changed so renderers re-upload
    /// all six faces on the next draw. Call this after mutating any
    /// face image.
    /// </summary>
    public void Invalidate()
    {
        unchecked { _version++; }
    }

    /// <summary>
    /// Returns the face image identified by <paramref name="face"/>.
    /// </summary>
    public Image GetFace(CubeFace face) => face switch
    {
        CubeFace.PositiveX => PositiveX,
        CubeFace.NegativeX => NegativeX,
        CubeFace.PositiveY => PositiveY,
        CubeFace.NegativeY => NegativeY,
        CubeFace.PositiveZ => PositiveZ,
        CubeFace.NegativeZ => NegativeZ,
        _ => throw new ArgumentOutOfRangeException(nameof(face), face, null),
    };

    /// <summary>
    /// Renders into one face of the cubemap using a 3D renderer,
    /// clearing the face first to <paramref name="backgroundColor"/>.
    /// The call is synchronous: when it returns, the face image's
    /// pixels reflect the final GPU output and the cubemap's
    /// <see cref="Version"/> has been bumped so the next bind
    /// re-uploads.
    /// </summary>
    /// <param name="face">The cubemap face to render into.</param>
    /// <param name="backgroundColor">The background painted behind the draws.</param>
    /// <param name="renderAction">Callback that issues draws on the renderer.</param>
    /// <remarks>
    /// Caller is responsible for setting the renderer's camera --
    /// typically a 90°-FOV <see cref="PerspectiveCamera"/> with
    /// <c>Target</c> / <c>Up</c> from <see cref="CubeFaceExtensions.GetForward"/>
    /// and <see cref="CubeFaceExtensions.GetUp"/>.
    /// </remarks>
    public void RenderFace(CubeFace face, Color backgroundColor, Action<Renderer3D> renderAction)
    {
        ArgumentNullException.ThrowIfNull(renderAction);
        GetBitmapFace(face).Render3D(backgroundColor, renderAction);
        Invalidate();
    }

    /// <summary>
    /// Renders into one face of the cubemap using a 3D renderer,
    /// preserving the face's existing pixels as a wallpaper behind
    /// the draws. The call is synchronous.
    /// </summary>
    /// <param name="face">The cubemap face to render into.</param>
    /// <param name="renderAction">Callback that issues draws on the renderer.</param>
    public void RenderFace(CubeFace face, Action<Renderer3D> renderAction)
    {
        ArgumentNullException.ThrowIfNull(renderAction);
        GetBitmapFace(face).Render3D(renderAction);
        Invalidate();
    }

    /// <summary>
    /// Renders all six faces in turn, clearing each first to
    /// <paramref name="backgroundColor"/> and invoking
    /// <paramref name="renderAction"/> with the active face. Call is
    /// synchronous; the cubemap is invalidated once at the end.
    /// </summary>
    /// <param name="backgroundColor">Background painted behind each face's draws.</param>
    /// <param name="renderAction">Callback invoked once per face.</param>
    public void RenderAllFaces(Color backgroundColor, Action<CubeFace, Renderer3D> renderAction)
    {
        ArgumentNullException.ThrowIfNull(renderAction);
        foreach (var face in CubeFaceExtensions.All)
            GetBitmapFace(face).Render3D(backgroundColor, rd => renderAction(face, rd));
        Invalidate();
    }

    // Render3D and CPU pixel access require a BitmapImage face. All
    // faces in today's API are constructed from BitmapImages (either
    // directly via Image x 6 or as MipmappedImage.Base levels), so the
    // cast is sound. Once GpuImage faces are supported the cast will
    // need a fallback path.
    private BitmapImage GetBitmapFace(CubeFace face)
    {
        var img = GetFace(face);
        if (img is not BitmapImage bitmap)
            throw new NotSupportedException(
                $"Cubemap face is not a {nameof(BitmapImage)} (got {img.GetType().Name}); " +
                $"CPU-side render and pixel operations require a BitmapImage base level.");
        return bitmap;
    }

    /// <summary>
    /// Builds a cubemap from six face images. All faces must have the
    /// same square pixel dimensions and the same
    /// <see cref="PixelFormat"/>.
    /// </summary>
    /// <param name="positiveX">Image for the +X face.</param>
    /// <param name="negativeX">Image for the -X face.</param>
    /// <param name="positiveY">Image for the +Y face.</param>
    /// <param name="negativeY">Image for the -Y face.</param>
    /// <param name="positiveZ">Image for the +Z face.</param>
    /// <param name="negativeZ">Image for the -Z face.</param>
    /// <param name="mipmaps">
    /// When <c>true</c>, renderers will generate a mip chain for the
    /// cubemap on upload. Defaults to <c>false</c>.
    /// </param>
    public static Cubemap Create(
        Image positiveX, Image negativeX,
        Image positiveY, Image negativeY,
        Image positiveZ, Image negativeZ,
        bool mipmaps = false)
    {
        ArgumentNullException.ThrowIfNull(positiveX);
        ArgumentNullException.ThrowIfNull(negativeX);
        ArgumentNullException.ThrowIfNull(positiveY);
        ArgumentNullException.ThrowIfNull(negativeY);
        ArgumentNullException.ThrowIfNull(positiveZ);
        ArgumentNullException.ThrowIfNull(negativeZ);

        return Create(
            MipmappedImage.FromBase(positiveX),
            MipmappedImage.FromBase(negativeX),
            MipmappedImage.FromBase(positiveY),
            MipmappedImage.FromBase(negativeY),
            MipmappedImage.FromBase(positiveZ),
            MipmappedImage.FromBase(negativeZ),
            mipmaps);
    }

    /// <summary>
    /// Builds a cubemap from six face mip chains. Each face must have
    /// a square base level, all faces must share their base level
    /// size, <see cref="PixelFormat"/>, and number of mip levels.
    /// </summary>
    /// <param name="positiveX">Mip chain for the +X face.</param>
    /// <param name="negativeX">Mip chain for the -X face.</param>
    /// <param name="positiveY">Mip chain for the +Y face.</param>
    /// <param name="negativeY">Mip chain for the -Y face.</param>
    /// <param name="positiveZ">Mip chain for the +Z face.</param>
    /// <param name="negativeZ">Mip chain for the -Z face.</param>
    /// <param name="mipmaps">
    /// When <c>true</c> AND each face is a single-level chain,
    /// renderers will auto-generate a mip chain by downsampling the
    /// base. Ignored when the supplied chains already have more than
    /// one level (the explicit content always wins).
    /// </param>
    public static Cubemap Create(
        MipmappedImage positiveX, MipmappedImage negativeX,
        MipmappedImage positiveY, MipmappedImage negativeY,
        MipmappedImage positiveZ, MipmappedImage negativeZ,
        bool mipmaps = false)
    {
        ArgumentNullException.ThrowIfNull(positiveX);
        ArgumentNullException.ThrowIfNull(negativeX);
        ArgumentNullException.ThrowIfNull(positiveY);
        ArgumentNullException.ThrowIfNull(negativeY);
        ArgumentNullException.ThrowIfNull(positiveZ);
        ArgumentNullException.ThrowIfNull(negativeZ);

        int w = positiveX.Width;
        int h = positiveX.Height;
        if (w != h)
            throw new ArgumentException(
                $"Cubemap faces must be square; +X base is {w}x{h}.",
                nameof(positiveX));

        var format = positiveX.PixelFormat;
        var levelCount = positiveX.LevelCount;
        var faces = new[]
        {
            (Name: "negativeX", Chain: negativeX),
            (Name: "positiveY", Chain: positiveY),
            (Name: "negativeY", Chain: negativeY),
            (Name: "positiveZ", Chain: positiveZ),
            (Name: "negativeZ", Chain: negativeZ),
        };
        foreach (var (name, chain) in faces)
        {
            if (chain.Width != w || chain.Height != h)
                throw new ArgumentException(
                    $"All cubemap faces must share base size; +X is {w}x{h} but {name} is {chain.Width}x{chain.Height}.",
                    name);
            if (chain.PixelFormat != format)
                throw new ArgumentException(
                    $"All cubemap faces must share PixelFormat; +X is {format} but {name} is {chain.PixelFormat}.",
                    name);
            if (chain.LevelCount != levelCount)
                throw new ArgumentException(
                    $"All cubemap faces must have the same mip-level count; +X has {levelCount} but {name} has {chain.LevelCount}.",
                    name);
        }

        // Explicit multi-level chains supersede the auto-generate flag.
        bool effectiveAutoMip = mipmaps && levelCount == 1;
        return new Cubemap(positiveX, negativeX, positiveY, negativeY, positiveZ, negativeZ, effectiveAutoMip);
    }
}

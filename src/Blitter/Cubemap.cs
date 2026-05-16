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
/// orientation, use <see cref="Bitmap.Flip"/> and
/// <see cref="Bitmap.Rotate"/> on each face before passing it in.
/// </para>
/// <para>
/// Like <see cref="Texture2D"/>, a <c>Cubemap</c> is a CPU-side handle.
/// Renderers upload it to the GPU lazily and cache the result, keyed
/// on <see cref="Version"/>. Bumping <see cref="Version"/> -- by
/// reassigning a face image's pixels and calling
/// <see cref="Invalidate"/> -- forces the next draw to re-upload all
/// six faces.
/// </para>
/// </remarks>
public sealed class Cubemap : TextureCube
{
    private int _version;

    private Cubemap(
        Mipmap positiveX, Mipmap negativeX,
        Mipmap positiveY, Mipmap negativeY,
        Mipmap positiveZ, Mipmap negativeZ,
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
    public Texture2D PositiveX => PositiveXLevels.Base;
    /// <summary>The -X face: looking toward world -X (left).</summary>
    public Texture2D NegativeX => NegativeXLevels.Base;
    /// <summary>The +Y face: looking toward world +Y (up).</summary>
    public Texture2D PositiveY => PositiveYLevels.Base;
    /// <summary>The -Y face: looking toward world -Y (down).</summary>
    public Texture2D NegativeY => NegativeYLevels.Base;
    /// <summary>The +Z face: looking toward world +Z (forward in a left-handed system, backward in a right-handed system).</summary>
    public Texture2D PositiveZ => PositiveZLevels.Base;
    /// <summary>The -Z face: looking toward world -Z.</summary>
    public Texture2D NegativeZ => NegativeZLevels.Base;

    /// <summary>The +X face's mip chain.</summary>
    public Mipmap PositiveXLevels { get; }
    /// <summary>The -X face's mip chain.</summary>
    public Mipmap NegativeXLevels { get; }
    /// <summary>The +Y face's mip chain.</summary>
    public Mipmap PositiveYLevels { get; }
    /// <summary>The -Y face's mip chain.</summary>
    public Mipmap NegativeYLevels { get; }
    /// <summary>The +Z face's mip chain.</summary>
    public Mipmap PositiveZLevels { get; }
    /// <summary>The -Z face's mip chain.</summary>
    public Mipmap NegativeZLevels { get; }

    /// <summary>
    /// Edge length of every face's base mip level, in pixels. All six
    /// faces are square and share this size.
    /// </summary>
    public override int Size { get; }

    /// <summary>
    /// Pixel format shared by all six faces.
    /// </summary>
    public override PixelFormat Format { get; }

    /// <summary>
    /// Number of mip levels supplied per face. <c>1</c> means only a
    /// base level was provided. When <c>&gt; 1</c>, every face's
    /// <see cref="Mipmap.LevelCount"/> is this same value.
    /// </summary>
    public override int LevelCount { get; }

    /// <summary>
    /// When <c>true</c>, hints to renderers to generate a full mipmap
    /// chain for the cubemap. Required for image-based lighting and
    /// helps reduce shimmer when sampled at oblique angles.
    /// </summary>
    public override bool Mipmaps { get; }

    /// <summary>
    /// Bumped each time the cubemap's contents change. Renderers use
    /// this to detect when their cached GPU upload is stale.
    /// </summary>
    public override int Version => _version;

    /// <summary>
    /// Marks the cubemap contents as changed so renderers re-upload
    /// all six faces on the next draw. Call this after mutating any
    /// face image.
    /// </summary>
    public override void Invalidate()
    {
        unchecked { _version++; }
    }

    /// <summary>
    /// Returns the face image identified by <paramref name="face"/>.
    /// </summary>
    public Texture2D GetFace(CubeFace face) => face switch
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
    /// Renders all six faces in turn, clearing each first to
    /// <paramref name="backgroundColor"/> and invoking
    /// <paramref name="drawAction"/> with the active face. Call is
    /// synchronous; the cubemap is invalidated once at the end.
    /// </summary>
    /// <param name="backgroundColor">Background painted behind each face's draws.</param>
    /// <param name="drawAction">Callback invoked once per face.</param>
    public void Render(Color backgroundColor, Action<Renderer3D, CubeFace> drawAction)
    {
        ArgumentNullException.ThrowIfNull(drawAction);
        foreach (var face in CubeFaceExtensions.All)
            GetBitmapFace(face).Render3D(backgroundColor, rd => drawAction(rd, face));
        Invalidate();
    }

    /// <summary>
    /// Renders all six faces in turn, preserving each face's
    /// existing pixels as a wallpaper behind the draws. Call is
    /// synchronous; the cubemap is invalidated once at the end.
    /// </summary>
    /// <param name="drawAction">Callback invoked once per face.</param>
    public void Render(Action<Renderer3D, CubeFace> drawAction)
    {
        ArgumentNullException.ThrowIfNull(drawAction);
        foreach (var face in CubeFaceExtensions.All)
            GetBitmapFace(face).Render3D(rd => drawAction(rd, face));
        Invalidate();
    }

    // Render3D and CPU pixel access require a Bitmap face. All
    // faces in today's API are constructed from Bitmaps (either
    // directly via Image x 6 or as MipmappedImage.Base levels), so the
    // cast is sound. Once GpuBitmap faces are supported the cast will
    // need a fallback path.
    private Bitmap GetBitmapFace(CubeFace face)
    {
        var img = GetFace(face);
        if (img is not Bitmap bitmap)
            throw new NotSupportedException(
                $"Cubemap face is not a {nameof(Bitmap)} (got {img.GetType().Name}); " +
                $"CPU-side render and pixel operations require a Bitmap base level.");
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
        Texture2D positiveX, Texture2D negativeX,
        Texture2D positiveY, Texture2D negativeY,
        Texture2D positiveZ, Texture2D negativeZ,
        bool mipmaps = false)
    {
        ArgumentNullException.ThrowIfNull(positiveX);
        ArgumentNullException.ThrowIfNull(negativeX);
        ArgumentNullException.ThrowIfNull(positiveY);
        ArgumentNullException.ThrowIfNull(negativeY);
        ArgumentNullException.ThrowIfNull(positiveZ);
        ArgumentNullException.ThrowIfNull(negativeZ);

        return Create(
            Mipmap.FromBase(positiveX),
            Mipmap.FromBase(negativeX),
            Mipmap.FromBase(positiveY),
            Mipmap.FromBase(negativeY),
            Mipmap.FromBase(positiveZ),
            Mipmap.FromBase(negativeZ),
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
        Mipmap positiveX, Mipmap negativeX,
        Mipmap positiveY, Mipmap negativeY,
        Mipmap positiveZ, Mipmap negativeZ,
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

namespace Grape;

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
/// same <see cref="Grape.PixelFormat"/>.
/// </para>
/// <para>
/// Face image orientation follows the Direct3D / Vulkan / Metal
/// convention: pixel (0, 0) of each face image is the upper-left
/// corner as seen from the cube's center looking outward through that
/// face. If your source images come from a pack with a different
/// orientation, use <see cref="Image.Flip"/> and
/// <see cref="Image.Rotate"/> on each face before passing it in.
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
        Image positiveX, Image negativeX,
        Image positiveY, Image negativeY,
        Image positiveZ, Image negativeZ,
        bool mipmaps)
    {
        PositiveX = positiveX;
        NegativeX = negativeX;
        PositiveY = positiveY;
        NegativeY = negativeY;
        PositiveZ = positiveZ;
        NegativeZ = negativeZ;
        Mipmaps = mipmaps;
        _version = 1;

        var (size, _) = positiveX.Size;
        Size = size;
        Format = positiveX.PixelFormat;
    }

    /// <summary>The +X face: looking toward world +X (right).</summary>
    public Image PositiveX { get; }
    /// <summary>The -X face: looking toward world -X (left).</summary>
    public Image NegativeX { get; }
    /// <summary>The +Y face: looking toward world +Y (up).</summary>
    public Image PositiveY { get; }
    /// <summary>The -Y face: looking toward world -Y (down).</summary>
    public Image NegativeY { get; }
    /// <summary>The +Z face: looking toward world +Z (forward in a left-handed system, backward in a right-handed system).</summary>
    public Image PositiveZ { get; }
    /// <summary>The -Z face: looking toward world -Z.</summary>
    public Image NegativeZ { get; }

    /// <summary>
    /// Edge length of every face, in pixels. All six faces are square
    /// and share this size.
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// Pixel format shared by all six faces.
    /// </summary>
    public PixelFormat Format { get; }

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

        var (w, h) = positiveX.Size;
        if (w != h)
            throw new ArgumentException(
                $"Cubemap faces must be square; +X face is {w}x{h}.",
                nameof(positiveX));

        var format = positiveX.PixelFormat;
        var faces = new[]
        {
            (Name: "negativeX", Image: negativeX),
            (Name: "positiveY", Image: positiveY),
            (Name: "negativeY", Image: negativeY),
            (Name: "positiveZ", Image: positiveZ),
            (Name: "negativeZ", Image: negativeZ),
        };
        foreach (var (name, image) in faces)
        {
            var (fw, fh) = image.Size;
            if (fw != w || fh != h)
                throw new ArgumentException(
                    $"All cubemap faces must have the same dimensions; +X is {w}x{h} but {name} is {fw}x{fh}.",
                    name);
            if (image.PixelFormat != format)
                throw new ArgumentException(
                    $"All cubemap faces must share the same PixelFormat; +X is {format} but {name} is {image.PixelFormat}.",
                    name);
        }

        return new Cubemap(positiveX, negativeX, positiveY, negativeY, positiveZ, negativeZ, mipmaps);
    }
}

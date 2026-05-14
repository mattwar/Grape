namespace Blitter;

/// <summary>
/// Base type for any 2D image asset the renderer can sample as a
/// texture. Concrete subtypes include <see cref="Bitmap"/>
/// (CPU-backed surface with full pixel access) and
/// <see cref="MipmappedImage"/> (an explicit mip chain of images).
/// </summary>
public abstract class Image : Texture, IDisposable
{
    /// <summary>Width of the (base level of the) image in pixels.</summary>
    public abstract int Width { get; }

    /// <summary>Height of the (base level of the) image in pixels.</summary>
    public abstract int Height { get; }

    /// <summary>Pixel format of the image.</summary>
    public abstract PixelFormat PixelFormat { get; }

    /// <summary>
    /// Bumped each time the image's contents change. Renderers use
    /// this to detect when their cached GPU upload is stale.
    /// </summary>
    public abstract int Version { get; }

    /// <summary>
    /// Number of mip levels stored. <c>1</c> means just a base level.
    /// </summary>
    public abstract int LevelCount { get; }

    /// <summary>
    /// Hints to renderers that a mip chain should be generated for
    /// this image on upload. Ignored when <see cref="LevelCount"/>
    /// is already greater than 1.
    /// </summary>
    public abstract bool Mipmaps { get; }

    /// <summary>Whether the image has been disposed.</summary>
    public abstract bool IsDisposed { get; }

    /// <summary>Marks contents as changed so renderers re-upload.</summary>
    public abstract void Invalidate();

    /// <inheritdoc/>
    public abstract void Dispose();

    /// <summary>Size of the (base level of the) image in pixels.</summary>
    public (int Width, int Height) Size => (Width, Height);

    /// <summary>
    /// Creates a new in-memory <see cref="Bitmap"/> of the given
    /// size. Shortcut for <see cref="Bitmap.Create"/>.
    /// </summary>
    public static Bitmap Create(int width, int height, PixelFormat format = PixelFormat.ABGR8888, bool mipmaps = false)
        => Bitmap.Create(width, height, format, mipmaps);

    /// <summary>
    /// Loads a <see cref="Bitmap"/> from disk. Shortcut for
    /// <see cref="Bitmap.Load"/>.
    /// </summary>
    public static Bitmap Load(string filePath, bool mipmaps = false)
        => Bitmap.Load(filePath, mipmaps);

    /// <summary>
    /// Decodes a <see cref="Bitmap"/> from encoded bytes.
    /// Shortcut for <see cref="Bitmap.Decode"/>.
    /// </summary>
    public static Bitmap Decode(ReadOnlySpan<byte> bytes, bool mipmaps = false)
        => Bitmap.Decode(bytes, mipmaps);
}

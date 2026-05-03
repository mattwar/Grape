namespace Grape;

/// <summary>
/// A 2D renderer that draws into an <see cref="Image"/> in CPU memory using
/// SDL's software renderer. Pixels written by this renderer land directly
/// in the image's surface.
/// </summary>
internal sealed class ImageRenderer2D : BitmapRenderer2D
{
    private readonly Image _image;

    private ImageRenderer2D(Image image, nint rendererId)
        : base(rendererId)
    {
        _image = image;
    }

    /// <summary>
    /// Creates a software renderer that draws into <paramref name="image"/>.
    /// </summary>
    public static ImageRenderer2D Create(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        image.ThrowIfDisposed();

        _ = Application.Current;
        SDL.InitSubSystem(SDL.InitFlags.Video);

        var rendererId = SDL.CreateSoftwareRenderer(image._imageId);
        if (rendererId == 0)
            throw new InvalidOperationException(
                $"Failed to create software renderer for image: {SDL.GetError()}");

        return new ImageRenderer2D(image, rendererId);
    }

    /// <summary>The <see cref="Grape.Image"/> this renderer draws into.</summary>
    public Image Image => _image;

    protected override void OnDisposed()
    {
        // Pixels were written through SDL's renderer rather than the
        // version-tracked SetPixel path, so any cached GPU upload of this
        // image needs to re-stage on next use.
        if (!_image.IsDisposed)
            _image.Invalidate();
    }
}

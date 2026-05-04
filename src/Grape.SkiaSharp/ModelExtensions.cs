using System.ComponentModel;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace Grape;

public static class ModelExtensions
{
    extension(Image image)
    {
        /// <summary>
        /// Copies pixels from <paramref name="bitmap"/> into <paramref name="image"/>.
        /// Only the overlapping region (top-left aligned) is copied; pixels
        /// outside the smaller of the two are left untouched.
        /// Pixel format conversion is performed per pixel.
        /// </summary>
        public void CopyFromBitmap(SKBitmap bitmap)
        {
            ArgumentNullException.ThrowIfNull(bitmap);
            ObjectDisposedException.ThrowIf(image.IsDisposed, image);

            var (imageWidth, imageHeight) = image.Size;
            int width = Math.Min(bitmap.Width, imageWidth);
            int height = Math.Min(bitmap.Height, imageHeight);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var (r, g, b, a) = bitmap.GetPixel(x, y);
                    image.SetPixel(x, y, new Color(r, g, b, a));
                }
            }

            image.Invalidate();
        }

        /// <summary>
        /// Copies pixels from <paramref name="image"/> into <paramref name="bitmap"/>.
        /// Only the overlapping region (top-left aligned) is copied; pixels
        /// outside the smaller of the two are left untouched.
        /// Pixel format conversion is performed per pixel.
        /// </summary>
        public void CopyToBitmap(SKBitmap bitmap)
        {
            ArgumentNullException.ThrowIfNull(bitmap);
            ObjectDisposedException.ThrowIf(image.IsDisposed, image);

            var (imageWidth, imageHeight) = image.Size;
            int width = Math.Min(bitmap.Width, imageWidth);
            int height = Math.Min(bitmap.Height, imageHeight);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var (r, g, b, a) = image.GetPixel(x, y);
                    bitmap.SetPixel(x, y, new SKColor(r, g, b, a));
                }
            }
        }

        /// <summary>
        /// Renders image using SkiaSharp Canvas API.
        /// </summary>
        public void RenderCanvas(Action<SKCanvas> renderAction)
        {
            ArgumentNullException.ThrowIfNull(renderAction);
            ObjectDisposedException.ThrowIf(image.IsDisposed, image);

            var (width, height) = image.Size;
            if (width == 0 || height == 0)
                return;

            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            using var bitmap = new SKBitmap(info);

            image.CopyToBitmap(bitmap);

            using (var canvas = new SKCanvas(bitmap))
            {
                renderAction(canvas);
                canvas.Flush();
            }

            image.CopyFromBitmap(bitmap);
        }

        /// <summary>
        /// Creates a new <see cref="SKBitmap"/> the same size as the image
        /// and copies its pixels into it.
        /// </summary>
        public SKBitmap ToSKBitmap()
        {
            ObjectDisposedException.ThrowIf(image.IsDisposed, image);

            var (width, height) = image.Size;
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            var bitmap = new SKBitmap(info);
            image.CopyToBitmap(bitmap);
            return bitmap;
        }
    }

    extension(SKBitmap bitmap)
    {
        /// <summary>
        /// Maps the bitmap's <see cref="SKColorType"/> onto the closest
        /// <see cref="Grape.PixelFormat"/>. Returns
        /// <see cref="Grape.PixelFormat.BGRA8888"/> when the bitmap's
        /// color type has no direct equivalent.
        /// </summary>
        public Grape.PixelFormat PixelFormat => bitmap.ColorType switch
        {
            SKColorType.Bgra8888 => Grape.PixelFormat.BGRA8888,
            SKColorType.Rgba8888 => Grape.PixelFormat.RGBA8888,
            SKColorType.Rgb888x  => Grape.PixelFormat.XRGB8888,
            SKColorType.Rgb565   => Grape.PixelFormat.RGB565,
            SKColorType.Argb4444 => Grape.PixelFormat.ARGB4444,
            SKColorType.Gray8    => Grape.PixelFormat.Index8,
            _ => Grape.PixelFormat.BGRA8888,
        };

        /// <summary>
        /// Creates a new <see cref="Image"/> the same size as the bitmap
        /// and copies its pixels into it.
        /// </summary>
        public Image ToImage(Grape.PixelFormat? format = null)
        {
            ArgumentNullException.ThrowIfNull(bitmap);

            var image = Image.Create(bitmap.Width, bitmap.Height, format ?? bitmap.PixelFormat);
            image.CopyFromBitmap(bitmap);
            return image;
        }
    }

    extension(Image)
    {
        /// <summary>
        /// Creates an <see cref="Image"/> from an image file (.png, .jpg, etc)
        /// </summary>
        public static Image LoadImage(string filename, Grape.PixelFormat? format = null)
        {
            var bytes = File.ReadAllBytes(filename);
            return Decode(bytes, format);
        }

        /// <summary>
        /// Decodes an <see cref="Image"/> from a byte span containing encoded image data (.png, .jpg, etc)
        /// </summary>
        public static Image Decode(Span<byte> bytes, Grape.PixelFormat? format = null)
        {
            using var skBitmap = SKBitmap.Decode(bytes);
            if (skBitmap == null)
                throw new InvalidOperationException("Cannot decode image from bytes");

            return skBitmap.ToImage(format);
        }
    }

    extension (SKColor color)
    {
        public void Deconstruct(out byte r, out byte g, out byte b, out byte a)
        {
            r = color.Red;
            g = color.Green;
            b = color.Blue;
            a = color.Alpha;
        }

        public Color ToColor() => new(color.Red, color.Green, color.Blue, color.Alpha);
    }

    extension(Renderer2D renderer)
    {
        /// <summary>
        /// Renders a portion of <paramref name="bitmap"/> to a destination rectangle.
        /// </summary>
        public bool RenderBitmap(SKBitmap bitmap, Rect source, Rect destination)
        {
            ArgumentNullException.ThrowIfNull(bitmap);
            return renderer.RenderImage(GetOrCreateMirrorImage(bitmap), source, destination);
        }

        /// <summary>
        /// Renders the entire <paramref name="bitmap"/> to a destination rectangle.
        /// </summary>
        public bool RenderBitmap(SKBitmap bitmap, Rect destination)
        {
            ArgumentNullException.ThrowIfNull(bitmap);
            return renderer.RenderImage(GetOrCreateMirrorImage(bitmap), destination);
        }

        /// <summary>
        /// Renders the entire <paramref name="bitmap"/> at a position with optional uniform scale.
        /// </summary>
        public bool RenderBitmap(SKBitmap bitmap, float x, float y, float scale = 1.0f)
        {
            ArgumentNullException.ThrowIfNull(bitmap);
            return renderer.RenderImage(GetOrCreateMirrorImage(bitmap), x, y, scale);
        }
    }

    /// <summary>
    /// Caches the <see cref="Image"/> mirror of an <see cref="SKBitmap"/> so
    /// repeated <c>RenderBitmap</c> calls on the same bitmap reuse the same
    /// <see cref="Image"/> (and therefore the same cached GPU texture inside
    /// the renderer). The bitmap's contents are re-copied into the image on
    /// every call, since SkiaSharp does not expose a public change counter.
    /// </summary>
    private static readonly ConditionalWeakTable<SKBitmap, Image> _bitmapImageCache = new();

    private static Image GetOrCreateMirrorImage(SKBitmap bitmap)
    {
        if (!_bitmapImageCache.TryGetValue(bitmap, out var image)
            || image.IsDisposed
            || image.Size != (bitmap.Width, bitmap.Height))
        {
            if (image is not null && !image.IsDisposed)
                image.Dispose();
            image = bitmap.ToImage();
            _bitmapImageCache.AddOrUpdate(bitmap, image);
        }
        else
        {
            image.CopyFromBitmap(bitmap);
        }
        return image;
    }
}

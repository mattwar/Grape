using SkiaSharp;

namespace Grape;

public static class ModelExtensions
{
    extension(Image)
    {
        /// <summary>
        /// Creates an <see cref="Image"/> from an image file.
        /// </summary>
        public static Image LoadImage(string filename, Grape.PixelFormat format = Grape.PixelFormat.BGRA8888)
        {
            var bytes = File.ReadAllBytes(filename);
            return GetImageFromBytes(bytes, format);
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
    }

    /// <summary>
    /// Loads an image from bytes and creates an <see cref="Image"/> with the given pixel format.
    /// </summary>
    private static Image GetImageFromBytes(Span<byte> bytes, Grape.PixelFormat format)
    {
        using var skBitmap = SKBitmap.Decode(bytes);
        if (skBitmap == null)
            throw new InvalidOperationException("Cannot decode image from bytes");

        var width = skBitmap.Width;
        var height = skBitmap.Height;
        var image = Image.Create(width, height, format);

        SDL.Color sdColor = default;
        var bbp = skBitmap.BytesPerPixel;

        // copy pixels manually to ensure correct pixel format and colors
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                (sdColor.R, sdColor.G, sdColor.B, sdColor.A) = skBitmap.GetPixel(x, y);
                if (bbp == 3)
                    sdColor.A = 0xFF; // no alpha channel in 3 byte images.. make sure it is opaque
                image.SetPixel(x, y, sdColor);
            }
        }

        return image;
    }
}

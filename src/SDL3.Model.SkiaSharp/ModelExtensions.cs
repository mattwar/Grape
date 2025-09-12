using SkiaSharp;

namespace SDL3.Model;

public static class ModelExtensions
{
    extension(Surface)
    {
        /// <summary>
        /// Creates a SDL surface from an image file with a pixel format matching the window.
        /// </summary>
        public static Surface LoadImage(string filename, SDL.PixelFormat format = SDL.PixelFormat.BGRA8888)
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
    /// Loads an image from bytes and creates an SDL surface with a pixel format matching the window.
    /// </summary>
    private static Surface GetImageFromBytes(Span<byte> bytes, SDL.PixelFormat format)
    {
        using var skBitmap = SKBitmap.Decode(bytes);
        if (skBitmap == null)
            throw new InvalidOperationException("Cannot decode image from bytes");

        var width = skBitmap.Width;
        var height = skBitmap.Height;
        var surface = Surface.Create(width, height, format);

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
                surface.SetPixel(x, y, sdColor);
            }
        }

        return surface;
    }
}
namespace SDL3.Model;

public static class ColorExtensions
{
    public const int DefaultColorTolerance = 8;

    extension(SDL.Color color)
    {
        public void Deconstruct(out byte r, out byte g, out byte b, out byte a)
        {
            r = color.R;
            g = color.G;
            b = color.B;
            a = color.A;
        }

        public SDL.Color WithAlpha(byte alpha)
        {
            return new SDL.Color { R = color.R, G = color.G, B = color.B, A = alpha };
        }

        public bool IsClosedTo(SDL.Color other, int tolerance = DefaultColorTolerance)
        {
            int dr = color.R - other.R;
            int dg = color.G - other.G;
            int db = color.B - other.B;
            return dr * dr + dg * dg + db * db <= tolerance * tolerance;
        }
    }
}


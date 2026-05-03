namespace Grape;

public static class ColorExtensions
{
    public const int DefaultColorTolerance = 8;

    extension(Color color)
    {
        public void Deconstruct(out byte r, out byte g, out byte b, out byte a)
        {
            r = color.R;
            g = color.G;
            b = color.B;
            a = color.A;
        }

        public bool IsClosedTo(Color other, int tolerance = DefaultColorTolerance)
        {
            int dr = color.R - other.R;
            int dg = color.G - other.G;
            int db = color.B - other.B;
            return dr * dr + dg * dg + db * db <= tolerance * tolerance;
        }
    }
}


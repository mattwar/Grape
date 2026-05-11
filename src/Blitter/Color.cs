using System.Numerics;
using System.Runtime.InteropServices;

namespace Blitter;

/// <summary>
/// A 32-bit RGBA color, one byte per channel.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Color : IEquatable<Color>
{
    /// <summary>The default color tolerance used by <see cref="IsClosedTo"/> when none is specified.</summary>
    public const int DefaultColorTolerance = 8;

    public readonly byte R;
    public readonly byte G;
    public readonly byte B;
    public readonly byte A;

    public Color(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public static Color FromRgb(byte r, byte g, byte b) => new(r, g, b, 255);
    public static Color FromRgba(byte r, byte g, byte b, byte a) => new(r, g, b, a);

    /// <summary>
    /// Creates a color from HSV components. <paramref name="hue"/> wraps to
    /// [0, 1); <paramref name="saturation"/> and <paramref name="value"/>
    /// are clamped to [0, 1].
    /// </summary>
    public static Color FromHsv(float hue, float saturation, float value, byte alpha = 255)
    {
        hue -= MathF.Floor(hue);
        saturation = Math.Clamp(saturation, 0f, 1f);
        value = Math.Clamp(value, 0f, 1f);

        float c = value * saturation;
        float hh = hue * 6f;
        float x = c * (1f - MathF.Abs(hh % 2f - 1f));
        float r, g, b;
        switch ((int)hh)
        {
            case 0: r = c; g = x; b = 0; break;
            case 1: r = x; g = c; b = 0; break;
            case 2: r = 0; g = c; b = x; break;
            case 3: r = 0; g = x; b = c; break;
            case 4: r = x; g = 0; b = c; break;
            default: r = c; g = 0; b = x; break;
        }
        float m = value - c;
        return new Color(
            (byte)((r + m) * 255f),
            (byte)((g + m) * 255f),
            (byte)((b + m) * 255f),
            alpha);
    }

    public Color WithAlpha(byte alpha) => new(R, G, B, alpha);

    /// <summary>Returns this color with the red channel replaced.</summary>
    public Color WithRed(byte red) => new(red, G, B, A);

    /// <summary>Returns this color with the green channel replaced.</summary>
    public Color WithGreen(byte green) => new(R, green, B, A);

    /// <summary>Returns this color with the blue channel replaced.</summary>
    public Color WithBlue(byte blue) => new(R, G, blue, A);

    /// <summary>
    /// Extracts the HSV components of this color (hue and saturation
    /// and value all in [0, 1]). Alpha is returned as the fourth
    /// element of the tuple, also normalized to [0, 1].
    /// </summary>
    public (float Hue, float Saturation, float Value, float Alpha) ToHsv()
    {
        float r = R / 255f, g = G / 255f, b = B / 255f;
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float delta = max - min;

        float h;
        if (delta <= 0f) h = 0f;
        else if (max == r) h = ((g - b) / delta) % 6f;
        else if (max == g) h = (b - r) / delta + 2f;
        else h = (r - g) / delta + 4f;
        h /= 6f;
        if (h < 0f) h += 1f;

        float s = max <= 0f ? 0f : delta / max;
        return (h, s, max, A / 255f);
    }

    /// <summary>
    /// Linear interpolation between <paramref name="a"/> and
    /// <paramref name="b"/> per channel (RGB and alpha) at parameter
    /// <paramref name="t"/>. <paramref name="t"/> is clamped to [0, 1].
    /// Interpolation happens in 8-bit sRGB space; for perceptually
    /// uniform blending, convert to HSV first.
    /// </summary>
    public static Color Lerp(Color a, Color b, float t)
    {
        t = t < 0f ? 0f : (t > 1f ? 1f : t);
        return new Color(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t));
    }

    /// <summary>
    /// Returns a darker variant of this color by mixing toward black
    /// by <paramref name="amount"/> (0 = unchanged, 1 = pure black).
    /// Alpha is preserved.
    /// </summary>
    public Color Darken(float amount)
    {
        amount = amount < 0f ? 0f : (amount > 1f ? 1f : amount);
        float k = 1f - amount;
        return new Color((byte)(R * k), (byte)(G * k), (byte)(B * k), A);
    }

    /// <summary>
    /// Returns a lighter variant of this color by mixing toward white
    /// by <paramref name="amount"/> (0 = unchanged, 1 = pure white).
    /// Alpha is preserved.
    /// </summary>
    public Color Lighten(float amount)
    {
        amount = amount < 0f ? 0f : (amount > 1f ? 1f : amount);
        return new Color(
            (byte)(R + (255 - R) * amount),
            (byte)(G + (255 - G) * amount),
            (byte)(B + (255 - B) * amount),
            A);
    }

    /// <summary>
    /// Builds a color from a normalized RGBA <see cref="Vector4"/>
    /// (each channel in [0, 1]; values are clamped). Inverse of the
    /// implicit <see cref="Color"/>-to-<see cref="Vector4"/> conversion.
    /// </summary>
    public static Color FromVector4(Vector4 rgba) =>
        new(
            (byte)(Math.Clamp(rgba.X, 0f, 1f) * 255f),
            (byte)(Math.Clamp(rgba.Y, 0f, 1f) * 255f),
            (byte)(Math.Clamp(rgba.Z, 0f, 1f) * 255f),
            (byte)(Math.Clamp(rgba.W, 0f, 1f) * 255f));

    /// <summary>
    /// Parses a CSS-style color string. Accepted formats:
    /// <c>#rgb</c>, <c>#rgba</c>, <c>#rrggbb</c>, <c>#rrggbbaa</c>
    /// (with or without the leading <c>#</c>), and
    /// <c>rgb(r,g,b)</c> / <c>rgba(r,g,b,a)</c> where alpha is
    /// either 0..255 or a float in [0, 1].
    /// </summary>
    public static Color Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (TryParse(text, out var color)) return color;
        throw new FormatException($"Could not parse color: \"{text}\".");
    }

    /// <inheritdoc cref="Parse(string)"/>
    public static bool TryParse(string? text, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim();

        if (s.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            return TryParseFunctional(s, out color);

        if (s[0] == '#') s = s[1..];
        if (!IsHex(s)) return false;
        return s.Length switch
        {
            3 => TrySetHex(out color, ExpandNibble(s[0]), ExpandNibble(s[1]), ExpandNibble(s[2]), 255),
            4 => TrySetHex(out color, ExpandNibble(s[0]), ExpandNibble(s[1]), ExpandNibble(s[2]), ExpandNibble(s[3])),
            6 => TrySetHex(out color, ByteHex(s, 0), ByteHex(s, 2), ByteHex(s, 4), 255),
            8 => TrySetHex(out color, ByteHex(s, 0), ByteHex(s, 2), ByteHex(s, 4), ByteHex(s, 6)),
            _ => false,
        };

        static bool IsHex(string v)
        {
            foreach (var c in v)
                if (!Uri.IsHexDigit(c)) return false;
            return true;
        }
        static int ExpandNibble(char c) { var n = HexNibble(c); return (n << 4) | n; }
        static int HexNibble(char c) => c <= '9' ? c - '0' : (c & ~0x20) - 'A' + 10;
        static int ByteHex(string v, int i) => (HexNibble(v[i]) << 4) | HexNibble(v[i + 1]);
        static bool TrySetHex(out Color c, int r, int g, int b, int a)
        { c = new Color((byte)r, (byte)g, (byte)b, (byte)a); return true; }
    }

    private static bool TryParseFunctional(string s, out Color color)
    {
        color = default;
        var open = s.IndexOf('(');
        var close = s.IndexOf(')');
        if (open < 0 || close <= open) return false;
        var inside = s[(open + 1)..close];
        var parts = inside.Split(',');
        if (parts.Length is not 3 and not 4) return false;

        if (!TryParseByteChannel(parts[0], out var r)) return false;
        if (!TryParseByteChannel(parts[1], out var g)) return false;
        if (!TryParseByteChannel(parts[2], out var b)) return false;
        byte a = 255;
        if (parts.Length == 4 && !TryParseAlphaChannel(parts[3], out a)) return false;

        color = new Color(r, g, b, a);
        return true;

        static bool TryParseByteChannel(string p, out byte v)
        {
            v = 0;
            if (!int.TryParse(p.Trim(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var n)) return false;
            if (n < 0 || n > 255) return false;
            v = (byte)n;
            return true;
        }

        static bool TryParseAlphaChannel(string p, out byte v)
        {
            v = 0;
            var t = p.Trim();
            // Accept either 0..255 integer or 0..1 float.
            if (t.Contains('.') || t.Contains('e', StringComparison.OrdinalIgnoreCase))
            {
                if (!float.TryParse(t, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var f)) return false;
                if (f < 0f || f > 1f) return false;
                v = (byte)(f * 255f);
                return true;
            }
            return TryParseByteChannel(t, out v);
        }
    }

    public void Deconstruct(out byte r, out byte g, out byte b, out byte a)
    {
        r = R;
        g = G;
        b = B;
        a = A;
    }

    /// <summary>
    /// Returns true if this color is within <paramref name="tolerance"/>
    /// (in squared RGB distance) of <paramref name="other"/>. Alpha is ignored.
    /// </summary>
    public bool IsClosedTo(Color other, int tolerance = DefaultColorTolerance)
    {
        int dr = R - other.R;
        int dg = G - other.G;
        int db = B - other.B;
        return dr * dr + dg * dg + db * db <= tolerance * tolerance;
    }

    public static readonly Color Transparent     = new(0, 0, 0, 0);
    public static readonly Color Black           = new(0, 0, 0);
    public static readonly Color White           = new(255, 255, 255);
    public static readonly Color Red             = new(255, 0, 0);
    public static readonly Color Green           = new(0, 255, 0);
    public static readonly Color Blue            = new(0, 0, 255);
    public static readonly Color Yellow          = new(255, 255, 0);
    public static readonly Color Cyan            = new(0, 255, 255);
    public static readonly Color Magenta         = new(255, 0, 255);
    public static readonly Color Gray            = new(128, 128, 128);
    public static readonly Color LightGray       = new(211, 211, 211);
    public static readonly Color DarkGray        = new(64, 64, 64);
    public static readonly Color Orange          = new(255, 165, 0);
    public static readonly Color Purple          = new(128, 0, 128);
    public static readonly Color Pink            = new(255, 192, 203);
    public static readonly Color Brown           = new(139, 69, 19);
    public static readonly Color Lime            = new(50, 205, 50);
    public static readonly Color CornflowerBlue  = new(100, 149, 237);

    public static implicit operator SDL.Color(Color c) => new() { R = c.R, G = c.G, B = c.B, A = c.A };
    public static implicit operator Color(SDL.Color c) => new(c.R, c.G, c.B, c.A);

    public static implicit operator SDL.FColor(Color c) =>
        new() { R = c.R / 255f, G = c.G / 255f, B = c.B / 255f, A = c.A / 255f };

    /// <summary>
    /// Converts to a normalized RGBA <see cref="Vector4"/> (each channel in 0..1),
    /// suitable for passing to shader uniforms.
    /// </summary>
    public static implicit operator Vector4(Color c) =>
        new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

    public bool Equals(Color other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override bool Equals(object? obj) => obj is Color other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    public static bool operator ==(Color a, Color b) => a.Equals(b);
    public static bool operator !=(Color a, Color b) => !a.Equals(b);

    public override string ToString() => $"Color(R={R}, G={G}, B={B}, A={A})";
}

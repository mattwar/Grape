using System.Numerics;
using System.Runtime.InteropServices;

namespace Grape;

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

    public Color WithAlpha(byte alpha) => new(R, G, B, alpha);

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

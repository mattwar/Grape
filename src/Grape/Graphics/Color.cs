using System.Runtime.InteropServices;

namespace Grape;

/// <summary>
/// A 32-bit RGBA color, one byte per channel.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Color : IEquatable<Color>
{
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

    public static readonly Color Transparent = new(0, 0, 0, 0);
    public static readonly Color Black       = new(0, 0, 0);
    public static readonly Color White       = new(255, 255, 255);
    public static readonly Color Red         = new(255, 0, 0);
    public static readonly Color Green       = new(0, 255, 0);
    public static readonly Color Blue        = new(0, 0, 255);
    public static readonly Color Yellow      = new(255, 255, 0);
    public static readonly Color Cyan        = new(0, 255, 255);
    public static readonly Color Magenta     = new(255, 0, 255);
    public static readonly Color Gray        = new(128, 128, 128);

    public static implicit operator SDL.Color(Color c) => new() { R = c.R, G = c.G, B = c.B, A = c.A };
    public static implicit operator Color(SDL.Color c) => new(c.R, c.G, c.B, c.A);

    public static implicit operator SDL.FColor(Color c) =>
        new() { R = c.R / 255f, G = c.G / 255f, B = c.B / 255f, A = c.A / 255f };

    public bool Equals(Color other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override bool Equals(object? obj) => obj is Color other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    public static bool operator ==(Color a, Color b) => a.Equals(b);
    public static bool operator !=(Color a, Color b) => !a.Equals(b);

    public override string ToString() => $"Color(R={R}, G={G}, B={B}, A={A})";
}

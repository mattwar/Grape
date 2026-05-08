using System.Numerics;
using System.Runtime.InteropServices;

namespace Grape;

/// <summary>
/// A rectangle defined by a position (top-left) and a size, using floating-point coordinates.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Rect : IEquatable<Rect>
{
    public readonly float X;
    public readonly float Y;
    public readonly float Width;
    public readonly float Height;

    public Rect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public Rect(Vector2 position, float width, float height)
        : this(position.X, position.Y, width, height) { }

    public float Left   => X;
    public float Top    => Y;
    public float Right  => X + Width;
    public float Bottom => Y + Height;

    public Vector2 Position => new(X, Y);

    public static implicit operator SDL.FRect(Rect r) => new() { X = r.X, Y = r.Y, W = r.Width, H = r.Height };
    public static implicit operator Rect(SDL.FRect r) => new(r.X, r.Y, r.W, r.H);

    public static implicit operator SDL.Rect(Rect r) => new() { X = (int)r.X, Y = (int)r.Y, W = (int)r.Width, H = (int)r.Height };
    public static implicit operator Rect(SDL.Rect r) => new(r.X, r.Y, r.W, r.H);

    public bool Equals(Rect other) =>
        X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is Rect other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public static bool operator ==(Rect a, Rect b) => a.Equals(b);
    public static bool operator !=(Rect a, Rect b) => !a.Equals(b);

    public override string ToString() => $"Rect(X={X}, Y={Y}, W={Width}, H={Height})";
}

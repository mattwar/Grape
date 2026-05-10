using System.Numerics;
using System.Runtime.InteropServices;

namespace Blitter;

/// <summary>
/// A 2D vertex used by <see cref="Renderer2D.DrawGeometry"/>: a position,
/// a tint color (linear float RGBA), and a texture coordinate.
/// </summary>
/// <remarks>
/// Layout is binary-compatible with SDL's <c>SDL_Vertex</c> so arrays can be
/// pinned and passed directly to the renderer without an intermediate copy.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Vertex2D : IEquatable<Vertex2D>, IPositionVertex2D
{
    /// <summary>The vertex position, in renderer coordinates.</summary>
    public readonly Vector2 Position;
    Vector2 IPositionVertex2D.Position => Position;

    /// <summary>The vertex tint color, as float RGBA in 0..1.</summary>
    public readonly Vector4 Color;

    /// <summary>The texture coordinate, in 0..1 (or larger if the sampler wraps).</summary>
    public readonly Vector2 TexCoord;

    public Vertex2D(Vector2 position, Vector4 color, Vector2 texCoord)
    {
        Position = position;
        Color = color;
        TexCoord = texCoord;
    }

    /// <summary>Convenience: construct from a byte <see cref="Blitter.Color"/>.</summary>
    public Vertex2D(Vector2 position, Color color, Vector2 texCoord)
        : this(position, ToVector4(color), texCoord)
    {
    }

    /// <summary>Convenience: untextured vertex with the given tint.</summary>
    public Vertex2D(Vector2 position, Color color)
        : this(position, ToVector4(color), Vector2.Zero)
    {
    }

    /// <summary>Convenience: untextured white vertex.</summary>
    public Vertex2D(Vector2 position)
        : this(position, new Vector4(1f, 1f, 1f, 1f), Vector2.Zero)
    {
    }

    private static Vector4 ToVector4(Color c) =>
        new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

    public bool Equals(Vertex2D other) =>
        Position.Equals(other.Position)
        && Color.Equals(other.Color)
        && TexCoord.Equals(other.TexCoord);

    public override bool Equals(object? obj) => obj is Vertex2D other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Position, Color, TexCoord);
    public static bool operator ==(Vertex2D a, Vertex2D b) => a.Equals(b);
    public static bool operator !=(Vertex2D a, Vertex2D b) => !a.Equals(b);
}

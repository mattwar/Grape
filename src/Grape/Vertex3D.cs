using System.Numerics;
using System.Runtime.InteropServices;

namespace Grape;

/// <summary>
/// A vertex with with just a position.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Vertex3D
{
    public readonly Vector3 Position;

    public Vertex3D(Vector3 position)
    {
        Position = position;
    }

    public Vertex3D(float x, float y, float z)
    {
        Position = new Vector3(x, y, z);
    }
}

/// <summary>
/// A vertex with position and color
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct ColorVertex3D
{
    public readonly Vector3 Position;
    public readonly SDL.Color Color;

    public ColorVertex3D(Vector3 position, SDL.Color color)
    {
        Position = position;
        Color = color;
    }

    public ColorVertex3D(Vertex3D vertex, SDL.Color color)
        : this(vertex.Position, color)
    {
    }
}

/// <summary>
/// A vertex that carries a position and a texture coordinate. Matches the
/// vertex input of the bundled <c>TexturedQuad</c> shaders.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct TextureVertex3D
{
    public readonly Vector3 Position;
    public readonly Vector2 TextureCoordinate;

    public TextureVertex3D(Vector3 position, Vector2 textureCoordinate)
    {
        Position = position;
        TextureCoordinate = textureCoordinate;
    }

    public TextureVertex3D(Vertex3D vertex, Vector2 textureCoordinate)
        : this(vertex.Position, textureCoordinate)
    {
    }
}

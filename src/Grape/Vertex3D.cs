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

    /// <summary>
    /// The shader-side vertex layout that pairs with this vertex struct.
    /// Reused by every <see cref="ShaderSet{TVertex}"/> built over
    /// <see cref="Vertex3D"/> so all such pipelines see the same attribute
    /// description.
    /// </summary>
    public static ShaderVertexLayout ShaderVertexLayout { get; } = new(
        ShaderVertexElementKind.Position3);
}

/// <summary>
/// A vertex with position and color
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct ColorVertex3D
{
    public readonly Vector3 Position;
    public readonly Color Color;

    public ColorVertex3D(Vector3 position, Color color)
    {
        Position = position;
        Color = color;
    }

    public ColorVertex3D(Vertex3D vertex, Color color)
        : this(vertex.Position, color)
    {
    }

    /// <summary>
    /// The shader-side vertex layout that pairs with this vertex struct.
    /// </summary>
    public static ShaderVertexLayout ShaderVertexLayout { get; } = new(
        ShaderVertexElementKind.Position3,
        ShaderVertexElementKind.Color4);
}

/// <summary>
/// A vertex that carries a position and a texture coordinate. Matches the
/// vertex input of the bundled <c>PositionTexture</c> shaders.
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

    /// <summary>
    /// The shader-side vertex layout that pairs with this vertex struct.
    /// </summary>
    public static ShaderVertexLayout ShaderVertexLayout { get; } = new(
        ShaderVertexElementKind.Position3,
        ShaderVertexElementKind.TextureCoordinate2);
}

/// <summary>
/// A vertex that carries a position, a world-space normal, and a baked
/// per-vertex color. Pairs with <see cref="Shaders.LitColor"/> -- the
/// shader runs Lambertian lighting against the renderer's
/// <see cref="Renderer3D.DirectionalLight"/> and
/// <see cref="Renderer3D.AmbientLight"/> using the normal, then modulates
/// the result by the per-vertex color.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct LitVertex3D
{
    public readonly Vector3 Position;
    public readonly Vector3 Normal;
    public readonly Color Color;

    public LitVertex3D(Vector3 position, Vector3 normal, Color color)
    {
        Position = position;
        Normal = normal;
        Color = color;
    }

    public LitVertex3D(Vertex3D vertex, Vector3 normal, Color color)
        : this(vertex.Position, normal, color)
    {
    }

    /// <summary>
    /// The shader-side vertex layout that pairs with this vertex struct.
    /// </summary>
    public static ShaderVertexLayout ShaderVertexLayout { get; } = new(
        ShaderVertexElementKind.Position3,
        ShaderVertexElementKind.Normal3,
        ShaderVertexElementKind.Color4);
}

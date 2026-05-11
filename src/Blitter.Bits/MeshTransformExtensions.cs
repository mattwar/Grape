using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Simple transformations of mesh vertices. 
/// For per-frame alterations use a transform at draw time instead.
/// </summary>
public static class MeshTransformExtensions
{
    public static Mesh<TVertex> Translate<TVertex>(this Mesh<TVertex> mesh, Vector3 offset)
        where TVertex : unmanaged =>
        TransformDispatch(mesh, Matrix4x4.CreateTranslation(offset));

    public static Mesh<TVertex> Translate<TVertex>(this Mesh<TVertex> mesh, float x, float y, float z)
        where TVertex : unmanaged =>
        TransformDispatch(mesh, Matrix4x4.CreateTranslation(x, y, z));

    public static Mesh<TVertex> Scale<TVertex>(this Mesh<TVertex> mesh, float scale)
        where TVertex : unmanaged =>
        TransformDispatch(mesh, Matrix4x4.CreateScale(scale));

    public static Mesh<TVertex> Scale<TVertex>(this Mesh<TVertex> mesh, Vector3 scale)
        where TVertex : unmanaged =>
        TransformDispatch(mesh, Matrix4x4.CreateScale(scale));

    public static Mesh<TVertex> Scale<TVertex>(this Mesh<TVertex> mesh, float x, float y, float z)
        where TVertex : unmanaged =>
        TransformDispatch(mesh, Matrix4x4.CreateScale(x, y, z));

    public static Mesh<TVertex> Rotate<TVertex>(this Mesh<TVertex> mesh, Quaternion rotation)
        where TVertex : unmanaged =>
        TransformDispatch(mesh, Matrix4x4.CreateFromQuaternion(rotation));

    public static Mesh<TVertex> RotateX<TVertex>(this Mesh<TVertex> mesh, float radians)
        where TVertex : unmanaged =>
        TransformDispatch(mesh, Matrix4x4.CreateRotationX(radians));

    public static Mesh<TVertex> RotateY<TVertex>(this Mesh<TVertex> mesh, float radians)
        where TVertex : unmanaged =>
        TransformDispatch(mesh, Matrix4x4.CreateRotationY(radians));

    public static Mesh<TVertex> RotateZ<TVertex>(this Mesh<TVertex> mesh, float radians)
        where TVertex : unmanaged =>
        TransformDispatch(mesh, Matrix4x4.CreateRotationZ(radians));

    // Dispatches to the correct typed Transform overload in MeshExtensions
    // so normals get the inverse-transpose treatment for lit vertex types.
    private static Mesh<TVertex> TransformDispatch<TVertex>(Mesh<TVertex> mesh, Matrix4x4 m)
        where TVertex : unmanaged
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return mesh switch
        {
            Mesh<Vertex3D>           v   => (Mesh<TVertex>)(object)v.Transform(m),
            Mesh<ColorVertex3D>      cv  => (Mesh<TVertex>)(object)cv.Transform(m),
            Mesh<TextureVertex3D>    tv  => (Mesh<TVertex>)(object)tv.Transform(m),
            Mesh<LitVertex3D>        lv  => (Mesh<TVertex>)(object)lv.Transform(m),
            Mesh<LitTextureVertex3D> ltv => (Mesh<TVertex>)(object)ltv.Transform(m),
            _ => throw new NotSupportedException(
                $"No Transform overload for vertex type {typeof(TVertex).Name}."),
        };
    }
}

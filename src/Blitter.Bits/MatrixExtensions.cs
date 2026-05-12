using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Chained, value-style helpers for composing <see cref="Matrix4x4"/>
/// and <see cref="Matrix3x2"/> transforms. Each method returns a new
/// matrix equal to <c>m * Matrix.CreateX(...)</c>, so a chain reads
/// in apply-order: <c>m.Scale(s).RotateZ(t).Translate(p)</c> means
/// <em>scale, then rotate, then translate</em>.
/// </summary>
public static class MatrixExtensions
{
    // --- Matrix4x4 ---------------------------------------------------

    public static Matrix4x4 Translate(this Matrix4x4 m, Vector3 offset) =>
        m * Matrix4x4.CreateTranslation(offset);

    public static Matrix4x4 Translate(this Matrix4x4 m, float x, float y, float z) =>
        m * Matrix4x4.CreateTranslation(x, y, z);

    public static Matrix4x4 Scale(this Matrix4x4 m, float scale) =>
        m * Matrix4x4.CreateScale(scale);

    public static Matrix4x4 Scale(this Matrix4x4 m, Vector3 scale) =>
        m * Matrix4x4.CreateScale(scale);

    public static Matrix4x4 Scale(this Matrix4x4 m, float x, float y, float z) =>
        m * Matrix4x4.CreateScale(x, y, z);

    public static Matrix4x4 RotateX(this Matrix4x4 m, float radians) =>
        m * Matrix4x4.CreateRotationX(radians);

    public static Matrix4x4 RotateY(this Matrix4x4 m, float radians) =>
        m * Matrix4x4.CreateRotationY(radians);

    public static Matrix4x4 RotateZ(this Matrix4x4 m, float radians) =>
        m * Matrix4x4.CreateRotationZ(radians);

    public static Matrix4x4 Rotate(this Matrix4x4 m, Quaternion rotation) =>
        m * Matrix4x4.CreateFromQuaternion(rotation);

    // --- Matrix3x2 ---------------------------------------------------

    public static Matrix3x2 Translate(this Matrix3x2 m, Vector2 offset) =>
        m * Matrix3x2.CreateTranslation(offset);

    public static Matrix3x2 Translate(this Matrix3x2 m, float x, float y) =>
        m * Matrix3x2.CreateTranslation(x, y);

    public static Matrix3x2 Scale(this Matrix3x2 m, float scale) =>
        m * Matrix3x2.CreateScale(scale);

    public static Matrix3x2 Scale(this Matrix3x2 m, Vector2 scale) =>
        m * Matrix3x2.CreateScale(scale);

    public static Matrix3x2 Scale(this Matrix3x2 m, float x, float y) =>
        m * Matrix3x2.CreateScale(x, y);

    public static Matrix3x2 Rotate(this Matrix3x2 m, float radians) =>
        m * Matrix3x2.CreateRotation(radians);

    public static Matrix3x2 Skew(this Matrix3x2 m, float radiansX, float radiansY) =>
        m * Matrix3x2.CreateSkew(radiansX, radiansY);
}

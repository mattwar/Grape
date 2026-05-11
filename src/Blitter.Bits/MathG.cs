using System.Numerics;
using System.Runtime.CompilerServices;

namespace Blitter.Bits;

/// <summary>
/// Scalar, vector, quaternion, and matrix helpers that fill the
/// gaps left by <c>System.Math</c>, <c>MathF</c>, and
/// <c>System.Numerics</c>. Nothing here duplicates a BCL API; use
/// the BCL for <c>Math.Clamp</c>, <c>float.Lerp</c>,
/// <c>Vector3.Reflect</c>, <c>Vector3.Refract</c>, etc.
/// </summary>
public static class MathG
{
    private const float DegToRad = MathF.PI / 180f;
    private const float RadToDeg = 180f / MathF.PI;

    /// <summary>Clamps <paramref name="value"/> to the [0, 1] range.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Saturate(float value) =>
        value < 0f ? 0f : (value > 1f ? 1f : value);

    /// <summary>
    /// Returns the parameter <c>t</c> such that
    /// <c>Lerp(a, b, t) == value</c>. Undefined when
    /// <paramref name="a"/> equals <paramref name="b"/>; returns 0
    /// in that degenerate case.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float InverseLerp(float a, float b, float value)
    {
        var d = b - a;
        return d == 0f ? 0f : (value - a) / d;
    }

    /// <summary>
    /// Remaps <paramref name="value"/> from the input range
    /// [<paramref name="srcMin"/>, <paramref name="srcMax"/>] to the
    /// output range [<paramref name="dstMin"/>, <paramref name="dstMax"/>].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Remap(float value, float srcMin, float srcMax, float dstMin, float dstMax) =>
        float.Lerp(dstMin, dstMax, InverseLerp(srcMin, srcMax, value));

    /// <summary>
    /// Hermite interpolation between 0 and 1 over the
    /// [<paramref name="edge0"/>, <paramref name="edge1"/>] window
    /// (matches HLSL <c>smoothstep</c>).
    /// </summary>
    public static float SmoothStep(float edge0, float edge1, float x)
    {
        var t = Saturate(InverseLerp(edge0, edge1, x));
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Ken Perlin's improved 5th-order variant of <see cref="SmoothStep"/>
    /// with zero first and second derivatives at the endpoints.
    /// </summary>
    public static float SmootherStep(float edge0, float edge1, float x)
    {
        var t = Saturate(InverseLerp(edge0, edge1, x));
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    /// <summary>
    /// Framerate-independent exponential smoothing toward
    /// <paramref name="target"/>. After <paramref name="halfLife"/>
    /// seconds the remaining gap is halved. Pass <c>dt</c> as the
    /// frame's delta time in seconds.
    /// </summary>
    public static float Damp(float current, float target, float halfLife, float dt)
    {
        if (halfLife <= 0f) return target;
        var t = 1f - MathF.Pow(0.5f, dt / halfLife);
        return current + (target - current) * t;
    }

    /// <inheritdoc cref="Damp(float, float, float, float)"/>
    public static Vector2 Damp(Vector2 current, Vector2 target, float halfLife, float dt)
    {
        if (halfLife <= 0f) return target;
        var t = 1f - MathF.Pow(0.5f, dt / halfLife);
        return Vector2.Lerp(current, target, t);
    }

    /// <inheritdoc cref="Damp(float, float, float, float)"/>
    public static Vector3 Damp(Vector3 current, Vector3 target, float halfLife, float dt)
    {
        if (halfLife <= 0f) return target;
        var t = 1f - MathF.Pow(0.5f, dt / halfLife);
        return Vector3.Lerp(current, target, t);
    }

    /// <summary>
    /// Moves <paramref name="current"/> toward <paramref name="target"/>
    /// by at most <paramref name="maxDelta"/>; stops exactly at the
    /// target. Use for constant-speed approach (UI bars, simple AI);
    /// pair with <see cref="Damp(float, float, float, float)"/> for
    /// inertial easing.
    /// </summary>
    public static float MoveToward(float current, float target, float maxDelta)
    {
        var diff = target - current;
        if (MathF.Abs(diff) <= maxDelta) return target;
        return current + MathF.Sign(diff) * maxDelta;
    }

    /// <inheritdoc cref="MoveToward(float, float, float)"/>
    public static Vector2 MoveToward(Vector2 current, Vector2 target, float maxDelta)
    {
        var diff = target - current;
        var len = diff.Length();
        if (len <= maxDelta || len == 0f) return target;
        return current + diff * (maxDelta / len);
    }

    /// <inheritdoc cref="MoveToward(float, float, float)"/>
    public static Vector3 MoveToward(Vector3 current, Vector3 target, float maxDelta)
    {
        var diff = target - current;
        var len = diff.Length();
        if (len <= maxDelta || len == 0f) return target;
        return current + diff * (maxDelta / len);
    }

    // -------------------- Angles --------------------

    /// <summary>Wraps an angle in degrees to the range [-180, 180).</summary>
    public static float WrapDegrees(float degrees)
    {
        degrees = (degrees + 180f) % 360f;
        if (degrees < 0f) degrees += 360f;
        return degrees - 180f;
    }

    /// <summary>Wraps an angle in radians to the range [-PI, PI).</summary>
    public static float WrapRadians(float radians)
    {
        const float twoPi = MathF.PI * 2f;
        radians = (radians + MathF.PI) % twoPi;
        if (radians < 0f) radians += twoPi;
        return radians - MathF.PI;
    }

    /// <summary>
    /// Signed shortest angular distance from <paramref name="from"/>
    /// to <paramref name="to"/> in degrees, in [-180, 180).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ShortestArcDegrees(float from, float to) => WrapDegrees(to - from);

    /// <summary>
    /// Signed shortest angular distance from <paramref name="from"/>
    /// to <paramref name="to"/> in radians, in [-PI, PI).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ShortestArcRadians(float from, float to) => WrapRadians(to - from);

    /// <summary>Converts degrees to radians.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DegreesToRadians(float degrees) => degrees * DegToRad;

    /// <summary>Converts radians to degrees.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float RadiansToDegrees(float radians) => radians * RadToDeg;

    // -------------------- Vector3 helpers --------------------

    /// <summary>
    /// Returns <paramref name="vector"/> with the component along
    /// <paramref name="planeNormal"/> removed. The normal does not
    /// need to be unit length.
    /// </summary>
    public static Vector3 ProjectOnPlane(Vector3 vector, Vector3 planeNormal)
    {
        var sqr = planeNormal.LengthSquared();
        if (sqr <= 0f) return vector;
        return vector - planeNormal * (Vector3.Dot(vector, planeNormal) / sqr);
    }

    /// <summary>
    /// Returns <paramref name="vector"/> clamped to a maximum length
    /// of <paramref name="maxLength"/>. Direction is preserved;
    /// shorter vectors pass through unchanged. Unlike
    /// <see cref="Vector3.Clamp(Vector3, Vector3, Vector3)"/>, which
    /// clamps each component independently.
    /// </summary>
    public static Vector3 ClampMagnitude(Vector3 vector, float maxLength)
    {
        var sqr = vector.LengthSquared();
        var maxSqr = maxLength * maxLength;
        if (sqr <= maxSqr) return vector;
        return vector * (maxLength / MathF.Sqrt(sqr));
    }

    /// <summary>
    /// Signed angle in radians from <paramref name="from"/> to
    /// <paramref name="to"/> around <paramref name="axis"/>, using
    /// the right-hand rule. Positive when the rotation from
    /// <c>from</c> to <c>to</c> follows the axis direction.
    /// </summary>
    public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
    {
        var fn = Vector3.Normalize(from);
        var tn = Vector3.Normalize(to);
        var cos = Math.Clamp(Vector3.Dot(fn, tn), -1f, 1f);
        var unsigned = MathF.Acos(cos);
        var sign = MathF.Sign(Vector3.Dot(Vector3.Cross(fn, tn), axis));
        return sign == 0f ? unsigned : unsigned * sign;
    }

    // -------------------- Quaternion / Matrix4x4 helpers --------------------

    /// <summary>
    /// Quaternion that aligns local -Z with <paramref name="forward"/>
    /// and local +Y as close to <paramref name="up"/> as possible.
    /// Matches Blitter's right-handed, -Z-forward camera convention.
    /// Returns <see cref="Quaternion.Identity"/> if
    /// <paramref name="forward"/> is zero-length.
    /// </summary>
    public static Quaternion LookRotation(Vector3 forward, Vector3 up)
    {
        var fwdLen = forward.Length();
        if (fwdLen <= 0f) return Quaternion.Identity;
        var f = forward / fwdLen;

        // World-space basis for the rotated object: local +Z points
        // along -forward (since the object looks down its own -Z).
        var z = -f;
        var right = Vector3.Cross(up, z);
        var rightLen = right.Length();
        if (rightLen <= 0f)
        {
            // forward parallel to up; pick an arbitrary perpendicular.
            var fallback = MathF.Abs(z.Y) < 0.999f ? Vector3.UnitY : Vector3.UnitX;
            right = Vector3.Cross(fallback, z);
            rightLen = right.Length();
        }
        var x = right / rightLen;
        var y = Vector3.Cross(z, x);

        // Row i of the upper 3x3 = world direction of local axis i.
        // (System.Numerics uses row-vector convention `v' = v * M`.)
        var m = new Matrix4x4(
            x.X, x.Y, x.Z, 0f,
            y.X, y.Y, y.Z, 0f,
            z.X, z.Y, z.Z, 0f,
            0f, 0f, 0f, 1f);
        return Quaternion.CreateFromRotationMatrix(m);
    }

    /// <summary>
    /// Composes a translation-rotation-scale matrix in a single call.
    /// Scale is applied first, then rotation, then translation
    /// (the conventional model-matrix order).
    /// </summary>
    public static Matrix4x4 TRS(Vector3 translation, Quaternion rotation, Vector3 scale) =>
        Matrix4x4.CreateScale(scale)
        * Matrix4x4.CreateFromQuaternion(rotation)
        * Matrix4x4.CreateTranslation(translation);

    /// <inheritdoc cref="TRS(Vector3, Quaternion, Vector3)"/>
    public static Matrix4x4 TRS(Vector3 translation, Quaternion rotation, float uniformScale) =>
        Matrix4x4.CreateScale(uniformScale)
        * Matrix4x4.CreateFromQuaternion(rotation)
        * Matrix4x4.CreateTranslation(translation);
}

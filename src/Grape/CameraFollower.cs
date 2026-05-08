using System.Numerics;

namespace Grape;

/// <summary>
/// Smoothly follows a moving <see cref="Target"/> in world space. The
/// camera sits at <see cref="Target"/> + <see cref="Offset"/> and
/// always looks at <see cref="Target"/>. Both the camera position and
/// the look-at point are exponentially smoothed to take the snap out
/// of fast target motion.
/// </summary>
public sealed class CameraFollower : CameraController
{
    private Vector3 _smoothedTarget;
    private Vector3 _smoothedPosition;
    private bool _initialized;

    /// <summary>World-space point being tracked.</summary>
    public Vector3 Target { get; set; } = Vector3.Zero;

    /// <summary>Offset from <see cref="Target"/> to the camera.</summary>
    public Vector3 Offset { get; set; } = new(0f, 2f, 5f);

    /// <summary>World-up axis used to anchor the look-at.</summary>
    public Vector3 Up { get; set; } = Vector3.UnitY;

    /// <summary>
    /// Position-smoothing time constant in seconds: roughly the time it
    /// takes the camera to cover ~63% of the distance to its goal.
    /// 0 = no smoothing (snap to target).
    /// </summary>
    public float PositionSmoothing { get; set; } = 0.25f;

    /// <summary>
    /// Look-at smoothing time constant in seconds. Usually a touch
    /// shorter than <see cref="PositionSmoothing"/> so the camera "leads"
    /// less and the framing stays tighter on the subject.
    /// </summary>
    public float LookSmoothing { get; set; } = 0.1f;

    public override void Update(in UpdateContext3D context)
    {
        var goalPosition = Target + Offset;

        if (!_initialized)
        {
            _smoothedTarget = Target;
            _smoothedPosition = goalPosition;
            _initialized = true;
        }
        else
        {
            var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
            _smoothedPosition = ExpSmooth(_smoothedPosition, goalPosition, PositionSmoothing, dt);
            _smoothedTarget = ExpSmooth(_smoothedTarget, Target, LookSmoothing, dt);
        }

        Camera.Position = _smoothedPosition;
        Camera.Target = _smoothedTarget;
        Camera.Up = Up;
    }

    /// <summary>Force the camera to its goal pose immediately, skipping smoothing.</summary>
    public void Snap()
    {
        _smoothedTarget = Target;
        _smoothedPosition = Target + Offset;
        _initialized = true;
    }

    // Frame-rate-independent exponential smoothing toward 'goal'. With
    // tau=0 (or very small dt/tau), this collapses to a hard snap.
    private static Vector3 ExpSmooth(Vector3 current, Vector3 goal, float tau, float dt)
    {
        if (tau <= 0f || dt <= 0f)
            return goal;
        var alpha = 1f - MathF.Exp(-dt / tau);
        return Vector3.Lerp(current, goal, alpha);
    }
}

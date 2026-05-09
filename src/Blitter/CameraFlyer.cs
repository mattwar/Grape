using System.Numerics;

namespace Blitter;

/// <summary>
/// Free 6-DOF "fly" camera. WASD pans along the camera's local XZ
/// plane (relative to its current heading), Q/E rises and falls, and
/// holding the configured <see cref="LookButton"/> while moving the
/// mouse rotates yaw/pitch. Roll stays at zero.
/// </summary>
public sealed class CameraFlyer : CameraController
{
    private readonly Window _window;
    private Vector2? _lastMousePos;

    /// <summary>Camera position in world space.</summary>
    public Vector3 Position { get; set; } = new(0f, 0f, 5f);

    /// <summary>World-up axis used for the look-at <see cref="Camera.Up"/>.</summary>
    public Vector3 Up { get; set; } = Vector3.UnitY;

    /// <summary>Yaw angle in radians (rotation around <see cref="Up"/>; 0 = looking down -Z).</summary>
    public float Yaw { get; set; } = 0f;

    /// <summary>Pitch angle in radians. Clamped to <see cref="MaxPitch"/>.</summary>
    public float Pitch { get; set; } = 0f;

    /// <summary>Maximum absolute pitch in radians; prevents flipping over the poles. Default ~85°.</summary>
    public float MaxPitch { get; set; } = MathF.PI / 2f - 0.05f;

    /// <summary>Movement speed in world units per second.</summary>
    public float MoveSpeed { get; set; } = 5f;

    /// <summary>Multiplier applied to <see cref="MoveSpeed"/> while <see cref="Key.LShift"/> is held.</summary>
    public float SprintMultiplier { get; set; } = 4f;

    /// <summary>Radians per pixel of mouse motion while looking.</summary>
    public float LookSpeed { get; set; } = 0.005f;

    /// <summary>Mouse button that, when held, drives look rotation. Default <see cref="MouseButton.Right"/>.</summary>
    public MouseButton LookButton { get; set; } = MouseButton.Right;

    public CameraFlyer(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
    }

    public override void Update(in UpdateContext3D context)
    {
        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;

        var pos = Mouse.GetPosition(_window);
        if (Mouse.IsDown(LookButton))
        {
            if (_lastMousePos is { } prev)
            {
                var delta = pos - prev;
                Yaw -= delta.X * LookSpeed;
                Pitch = Math.Clamp(Pitch - delta.Y * LookSpeed, -MaxPitch, MaxPitch);
            }
        }
        _lastMousePos = pos;

        // Forward direction from yaw + pitch (right-handed, -Z forward at yaw=0).
        var cosP = MathF.Cos(Pitch);
        var forward = new Vector3(
            -cosP * MathF.Sin(Yaw),
             MathF.Sin(Pitch),
            -cosP * MathF.Cos(Yaw));

        var right = Vector3.Normalize(Vector3.Cross(forward, Up));

        var move = Vector3.Zero;
        if (Keyboard.IsDown(Key.W)) move += forward;
        if (Keyboard.IsDown(Key.S)) move -= forward;
        if (Keyboard.IsDown(Key.D)) move += right;
        if (Keyboard.IsDown(Key.A)) move -= right;
        if (Keyboard.IsDown(Key.E)) move += Up;
        if (Keyboard.IsDown(Key.Q)) move -= Up;

        if (move != Vector3.Zero)
        {
            var speed = MoveSpeed;
            if (Keyboard.IsDown(Key.LShift) || Keyboard.IsDown(Key.RShift))
                speed *= SprintMultiplier;
            Position += Vector3.Normalize(move) * speed * dt;
        }

        Camera.Position = Position;
        Camera.Target = Position + forward;
        Camera.Up = Up;
    }
}

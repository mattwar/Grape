using System.Numerics;

namespace Grape;

/// <summary>
/// First-person ground-walker camera. WASD slides along the world's
/// horizontal plane (movement ignores pitch, so looking down doesn't
/// dive into the floor), holding the configured <see cref="LookButton"/>
/// while moving the mouse rotates yaw/pitch, and Q/E nudges
/// <see cref="EyeHeight"/> up/down. Roll is locked at zero.
/// </summary>
public sealed class CameraWalker : CameraController
{
    private readonly Window _window;
    private Vector2? _lastMousePos;

    /// <summary>Walker position on the ground plane (Y = floor level).</summary>
    public Vector3 Position { get; set; } = Vector3.Zero;

    /// <summary>Distance from <see cref="Position"/> up to the eye.</summary>
    public float EyeHeight { get; set; } = 1.7f;

    /// <summary>World-up axis.</summary>
    public Vector3 Up { get; set; } = Vector3.UnitY;

    /// <summary>Yaw angle in radians (rotation around <see cref="Up"/>; 0 = looking down -Z).</summary>
    public float Yaw { get; set; } = 0f;

    /// <summary>Pitch angle in radians. Clamped to <see cref="MaxPitch"/>.</summary>
    public float Pitch { get; set; } = 0f;

    /// <summary>Maximum absolute pitch in radians; prevents flipping over the poles. Default ~85°.</summary>
    public float MaxPitch { get; set; } = MathF.PI / 2f - 0.05f;

    /// <summary>Walking speed in world units per second.</summary>
    public float MoveSpeed { get; set; } = 3f;

    /// <summary>Multiplier applied to <see cref="MoveSpeed"/> while <see cref="Key.LShift"/> is held.</summary>
    public float SprintMultiplier { get; set; } = 2f;

    /// <summary>Eye-height change in world units per second while Q/E held.</summary>
    public float HeightSpeed { get; set; } = 1f;

    /// <summary>Radians per pixel of mouse motion while looking.</summary>
    public float LookSpeed { get; set; } = 0.005f;

    /// <summary>Mouse button that, when held, drives look rotation. Default <see cref="MouseButton.Right"/>.</summary>
    public MouseButton LookButton { get; set; } = MouseButton.Right;

    public CameraWalker(Window window)
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

        // Look direction includes pitch.
        var cosP = MathF.Cos(Pitch);
        var look = new Vector3(
            -cosP * MathF.Sin(Yaw),
             MathF.Sin(Pitch),
            -cosP * MathF.Cos(Yaw));

        // Movement direction stays on the horizontal plane: yaw-only,
        // so looking down doesn't shorten the step.
        var forwardFlat = new Vector3(-MathF.Sin(Yaw), 0f, -MathF.Cos(Yaw));
        var rightFlat = Vector3.Normalize(Vector3.Cross(forwardFlat, Up));

        var move = Vector3.Zero;
        if (Keyboard.IsDown(Key.W)) move += forwardFlat;
        if (Keyboard.IsDown(Key.S)) move -= forwardFlat;
        if (Keyboard.IsDown(Key.D)) move += rightFlat;
        if (Keyboard.IsDown(Key.A)) move -= rightFlat;

        if (move != Vector3.Zero)
        {
            var speed = MoveSpeed;
            if (Keyboard.IsDown(Key.LShift) || Keyboard.IsDown(Key.RShift))
                speed *= SprintMultiplier;
            Position += Vector3.Normalize(move) * speed * dt;
        }

        if (Keyboard.IsDown(Key.E)) EyeHeight += HeightSpeed * dt;
        if (Keyboard.IsDown(Key.Q)) EyeHeight -= HeightSpeed * dt;

        var eye = Position + Up * EyeHeight;
        Camera.Position = eye;
        Camera.Target = eye + look;
        Camera.Up = Up;
    }
}

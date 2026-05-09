using System.Numerics;
using Blitter.Events;

namespace Blitter.Bits;

/// <summary>
/// Orbits the camera around <see cref="Target"/> via mouse drag, with
/// scroll-wheel zoom. Drag with the configured <see cref="DragButton"/>
/// to spin yaw/pitch; scroll to change <see cref="Distance"/>.
/// </summary>
/// <remarks>
/// Subscribes to the supplied <see cref="Window"/>'s
/// <see cref="Window.MouseWheel"/> event for the lifetime of the
/// process; there is no <c>Dispose</c> because controllers are
/// expected to live as long as the window does. If you need to
/// dispose, drop your reference and the event subscription will be
/// collected with the controller.
/// </remarks>
public sealed class CameraOrbiter : CameraController
{
    private readonly Window _window;
    private Vector2? _lastMousePos;
    private float _wheelAccum;

    /// <summary>Point in world space the camera orbits around.</summary>
    public Vector3 Target { get; set; } = Vector3.Zero;

    /// <summary>World-up axis used to anchor the orbit.</summary>
    public Vector3 Up { get; set; } = Vector3.UnitY;

    /// <summary>Distance from <see cref="Target"/> to the camera, in world units.</summary>
    public float Distance { get; set; } = 5f;

    /// <summary>Yaw angle in radians (rotation around <see cref="Up"/>).</summary>
    public float Yaw { get; set; } = 0f;

    /// <summary>Pitch angle in radians (rotation up/down). Clamped to <see cref="MaxPitch"/>.</summary>
    public float Pitch { get; set; } = 0.3f;

    /// <summary>Maximum absolute pitch in radians; prevents flipping over the poles. Default ~85°.</summary>
    public float MaxPitch { get; set; } = MathF.PI / 2f - 0.05f;

    /// <summary>Minimum allowed <see cref="Distance"/>.</summary>
    public float MinDistance { get; set; } = 0.5f;

    /// <summary>Maximum allowed <see cref="Distance"/>.</summary>
    public float MaxDistance { get; set; } = 1000f;

    /// <summary>Radians per pixel of mouse motion while dragging.</summary>
    public float RotateSpeed { get; set; } = 0.005f;

    /// <summary>Multiplier applied per scroll-wheel notch (1 = ±10% per notch).</summary>
    public float ZoomSpeed { get; set; } = 0.1f;

    /// <summary>Mouse button that, when held, drives orbit rotation.</summary>
    public MouseButton DragButton { get; set; } = MouseButton.Left;

    /// <summary>
    /// When true, dragging the mouse downward tilts the camera down
    /// (FPS-style). Default <see langword="false"/> uses the orbit/CAD
    /// convention where dragging down tilts the *subject* down — i.e.
    /// the camera arcs up and you see the top of the target.
    /// </summary>
    public bool InvertY { get; set; }

    public CameraOrbiter(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
        _window.MouseWheel += OnMouseWheel;
    }

    private void OnMouseWheel(Window sender, MouseWheelEventArgs e)
    {
        // Accumulate scroll across frames so a fast flick isn't lost
        // between Update() calls.
        _wheelAccum += e.Scroll.Y;
    }

    public override void Update(in UpdateContext3D context)
    {
        var pos = Mouse.GetPosition(_window);

        if (Mouse.IsDown(DragButton))
        {
            if (_lastMousePos is { } prev)
            {
                var delta = pos - prev;
                Yaw -= delta.X * RotateSpeed;
                var pitchDelta = delta.Y * RotateSpeed;
                if (InvertY) pitchDelta = -pitchDelta;
                Pitch = Math.Clamp(Pitch + pitchDelta, -MaxPitch, MaxPitch);
            }
        }
        _lastMousePos = pos;

        if (_wheelAccum != 0f)
        {
            // Multiplicative zoom feels uniform across distance scales.
            var factor = MathF.Pow(1f - ZoomSpeed, _wheelAccum);
            Distance = Math.Clamp(Distance * factor, MinDistance, MaxDistance);
            _wheelAccum = 0f;
        }

        // Spherical -> Cartesian. Yaw rotates around Up; Pitch lifts off
        // the horizon. With Pitch=0 the camera sits in the Target's
        // horizontal plane.
        var cosP = MathF.Cos(Pitch);
        var offset = new Vector3(
            Distance * cosP * MathF.Sin(Yaw),
            Distance * MathF.Sin(Pitch),
            Distance * cosP * MathF.Cos(Yaw));

        Camera.Position = Target + offset;
        Camera.Target = Target;
        Camera.Up = Up;
    }
}

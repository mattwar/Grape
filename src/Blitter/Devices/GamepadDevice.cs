using System.Numerics;

namespace Blitter.Devices;

/// <summary>
/// A connected gamepad device. Obtain instances from <see cref="Gamepad.Devices"/>,
/// <see cref="Gamepad.Primary"/>, or <see cref="Gamepad.ForPlayer(int)"/>. Polling
/// methods return safe defaults (false / zero) when the device has been
/// disconnected.
/// </summary>
public sealed class GamepadDevice : IDisposable
{
    private nint _handle;
    private readonly GamepadId _id;

    internal GamepadDevice(nint handle, GamepadId id)
    {
        _handle = handle;
        _id = id;
    }

    /// <summary>
    /// The stable joystick instance id for this device.
    /// </summary>
    public GamepadId Id => _id;

    /// <summary>
    /// True if the device is currently connected.
    /// </summary>
    public bool IsConnected => _handle != 0 && SDL.GamepadConnected(_handle);

    /// <summary>
    /// The implementation-dependent name of the device, or null if unavailable.
    /// </summary>
    public string? Name => _handle == 0 ? null : SDL.GetGamepadName(_handle);

    /// <summary>
    /// The 0-based player index assigned to this device, or null if not
    /// assigned.
    /// </summary>
    public int? PlayerIndex
    {
        get
        {
            if (_handle == 0)
                return null;
            var index = SDL.GetGamepadPlayerIndex(_handle);
            return index < 0 ? null : index;
        }
    }

    /// <summary>
    /// The recognized type of this device (Xbox/PS/Switch/etc).
    /// </summary>
    public GamepadType Type
        => _handle == 0 ? GamepadType.Unknown : (GamepadType)SDL.GetGamepadType(_handle);

    /// <summary>
    /// True if the device exposes the given button.
    /// </summary>
    public bool Has(GamepadButton button)
        => _handle != 0 && SDL.GamepadHasButton(_handle, (SDL.GamepadButton)(byte)button);

    /// <summary>
    /// True if the device exposes the given axis.
    /// </summary>
    public bool Has(GamepadAxis axis)
        => _handle != 0 && SDL.GamepadHasAxis(_handle, (SDL.GamepadAxis)(byte)axis);

    /// <summary>
    /// True if the given button is currently held down.
    /// </summary>
    public bool IsDown(GamepadButton button)
        => _handle != 0 && SDL.GetGamepadButton(_handle, (SDL.GamepadButton)(byte)button);

    /// <summary>
    /// A bitmask of all buttons currently held down.
    /// </summary>
    public GamepadButtons Buttons
    {
        get
        {
            if (_handle == 0)
                return GamepadButtons.None;
            GamepadButtons mask = 0;
            for (byte i = 0; i < (byte)SDL.GamepadButton.Count; i++)
            {
                if (SDL.GetGamepadButton(_handle, (SDL.GamepadButton)i))
                    mask |= (GamepadButtons)(1u << i);
            }
            return mask;
        }
    }

    /// <summary>
    /// Returns the current value of the given axis. Stick axes are normalized
    /// to [-1, 1] (Y positive points down, matching screen coordinates);
    /// trigger axes are normalized to [0, 1].
    /// </summary>
    public float GetAxis(GamepadAxis axis)
    {
        if (_handle == 0)
            return 0f;
        var raw = SDL.GetGamepadAxis(_handle, (SDL.GamepadAxis)(byte)axis);
        if (axis == GamepadAxis.LeftTrigger || axis == GamepadAxis.RightTrigger)
            return raw <= 0 ? 0f : raw / 32767f;
        return raw < 0 ? raw / 32768f : raw / 32767f;
    }

    /// <summary>
    /// The current left thumbstick position. X is left/right, Y is up/down
    /// (positive Y points down, matching screen coordinates).
    /// </summary>
    public Vector2 LeftStick => new(GetAxis(GamepadAxis.LeftX), GetAxis(GamepadAxis.LeftY));

    /// <summary>
    /// The current right thumbstick position. X is left/right, Y is up/down
    /// (positive Y points down, matching screen coordinates).
    /// </summary>
    public Vector2 RightStick => new(GetAxis(GamepadAxis.RightX), GetAxis(GamepadAxis.RightY));

    /// <summary>
    /// The current left trigger value, in [0, 1].
    /// </summary>
    public float LeftTrigger => GetAxis(GamepadAxis.LeftTrigger);

    /// <summary>
    /// The current right trigger value, in [0, 1].
    /// </summary>
    public float RightTrigger => GetAxis(GamepadAxis.RightTrigger);

    /// <summary>
    /// Starts a rumble effect on the low- and high-frequency motors.
    /// Both intensities are clamped to [0, 1]. Pass zero for both to stop
    /// rumble. Returns false if the device does not support rumble.
    /// </summary>
    public bool Rumble(float lowFrequency, float highFrequency, TimeSpan duration)
    {
        if (_handle == 0)
            return false;
        var lf = (ushort)(Math.Clamp(lowFrequency, 0f, 1f) * 0xFFFF);
        var hf = (ushort)(Math.Clamp(highFrequency, 0f, 1f) * 0xFFFF);
        var ms = ToDurationMs(duration);
        return SDL.RumbleGamepad(_handle, lf, hf, ms);
    }

    /// <summary>
    /// Starts a rumble effect on the trigger motors (Xbox One controllers and
    /// similar). Returns false if the device does not support trigger rumble.
    /// </summary>
    public bool RumbleTriggers(float left, float right, TimeSpan duration)
    {
        if (_handle == 0)
            return false;
        var l = (ushort)(Math.Clamp(left, 0f, 1f) * 0xFFFF);
        var r = (ushort)(Math.Clamp(right, 0f, 1f) * 0xFFFF);
        var ms = ToDurationMs(duration);
        return SDL.RumbleGamepadTriggers(_handle, l, r, ms);
    }

    /// <summary>
    /// Sets the LED color on devices that support a controllable LED
    /// (e.g. PS4 / PS5 light bar). Returns false if unsupported.
    /// </summary>
    public bool SetLed(byte red, byte green, byte blue)
        => _handle != 0 && SDL.SetGamepadLED(_handle, red, green, blue);

    private static uint ToDurationMs(TimeSpan duration)
    {
        var ms = duration.TotalMilliseconds;
        if (ms <= 0) return 0;
        if (ms >= uint.MaxValue) return uint.MaxValue;
        return (uint)ms;
    }

    /// <summary>
    /// Closes this device and releases the underlying SDL resources.
    /// </summary>
    public void Dispose()
    {
        var h = Interlocked.Exchange(ref _handle, 0);
        if (h != 0)
        {
            Gamepad.Forget(_id);
            SDL.CloseGamepad(h);
        }
    }
}

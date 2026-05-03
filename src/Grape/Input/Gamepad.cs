using System.Collections.Immutable;
using System.Numerics;

namespace Grape;

/// <summary>
/// A standard gamepad button. Values follow SDL's "south/east/west/north"
/// face-button convention (e.g. on Xbox: South=A, East=B, West=X, North=Y).
/// </summary>
public enum GamepadButton : byte
{
    South = 0,
    East,
    West,
    North,
    Back,
    Guide,
    Start,
    LeftStick,
    RightStick,
    LeftShoulder,
    RightShoulder,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    /// <summary>Additional button (e.g. Xbox Series X share, PS5 mic, Switch capture).</summary>
    Misc1,
    /// <summary>Upper / primary right-hand paddle (e.g. Xbox Elite P1).</summary>
    RightPaddle1,
    /// <summary>Upper / primary left-hand paddle (e.g. Xbox Elite P3).</summary>
    LeftPaddle1,
    /// <summary>Lower / secondary right-hand paddle (e.g. Xbox Elite P2).</summary>
    RightPaddle2,
    /// <summary>Lower / secondary left-hand paddle (e.g. Xbox Elite P4).</summary>
    LeftPaddle2,
    /// <summary>PS4 / PS5 touchpad button.</summary>
    Touchpad,
    Misc2,
    Misc3,
    Misc4,
    Misc5,
    Misc6,
}

/// <summary>
/// A bitmask of gamepad buttons currently held down. Bit position matches
/// the corresponding <see cref="GamepadButton"/> value.
/// </summary>
[Flags]
public enum GamepadButtons : uint
{
    None = 0,
    South        = 1u << 0,
    East         = 1u << 1,
    West         = 1u << 2,
    North        = 1u << 3,
    Back         = 1u << 4,
    Guide        = 1u << 5,
    Start        = 1u << 6,
    LeftStick    = 1u << 7,
    RightStick   = 1u << 8,
    LeftShoulder = 1u << 9,
    RightShoulder = 1u << 10,
    DPadUp       = 1u << 11,
    DPadDown     = 1u << 12,
    DPadLeft     = 1u << 13,
    DPadRight    = 1u << 14,
    Misc1        = 1u << 15,
    RightPaddle1 = 1u << 16,
    LeftPaddle1  = 1u << 17,
    RightPaddle2 = 1u << 18,
    LeftPaddle2  = 1u << 19,
    Touchpad     = 1u << 20,
    Misc2        = 1u << 21,
    Misc3        = 1u << 22,
    Misc4        = 1u << 23,
    Misc5        = 1u << 24,
    Misc6        = 1u << 25,
}

/// <summary>
/// A standard gamepad axis. Stick axes report values in [-1, 1]; trigger axes
/// report values in [0, 1].
/// </summary>
public enum GamepadAxis : byte
{
    LeftX = 0,
    LeftY,
    RightX,
    RightY,
    LeftTrigger,
    RightTrigger,
}

/// <summary>
/// The recognized standard gamepad type. Third-party controllers may report as
/// the layout they most closely match.
/// </summary>
public enum GamepadType
{
    Unknown = 0,
    Standard,
    Xbox360,
    XboxOne,
    PS3,
    PS4,
    PS5,
    NintendoSwitchPro,
    NintendoSwitchJoyConLeft,
    NintendoSwitchJoyConRight,
    NintendoSwitchJoyConPair,
    GameCube,
}

/// <summary>
/// Stable identifier for a connected gamepad. The same physical device retains
/// the same id for as long as it remains connected.
/// </summary>
public readonly record struct GamepadId(uint Value)
{
    public override string ToString() => Value.ToString();
}

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

/// <summary>
/// Polling access to connected gamepads.
/// </summary>
public static class Gamepad
{
    private static readonly object _sync = new();
    private static ImmutableDictionary<uint, GamepadDevice> _cache = ImmutableDictionary<uint, GamepadDevice>.Empty;
    private static bool _initialized;

    /// <summary>
    /// True if at least one gamepad is currently connected.
    /// </summary>
    public static bool HasGamepad
    {
        get
        {
            EnsureInit();
            return SDL.HasGamepad();
        }
    }

    /// <summary>
    /// Snapshot of the currently-connected gamepad devices. Devices are opened
    /// lazily on first access and cached; disconnected devices are pruned.
    /// </summary>
    public static IReadOnlyList<GamepadDevice> Devices
    {
        get
        {
            EnsureInit();
            return Refresh();
        }
    }

    /// <summary>
    /// The first connected gamepad, or null if none are connected. Convenience
    /// for the single-player case.
    /// </summary>
    public static GamepadDevice? Primary
    {
        get
        {
            var list = Devices;
            return list.Count == 0 ? null : list[0];
        }
    }

    /// <summary>
    /// Returns the connected gamepad assigned to the given 0-based player
    /// index, or null if none matches.
    /// </summary>
    public static GamepadDevice? ForPlayer(int playerIndex)
    {
        foreach (var d in Devices)
        {
            if (d.PlayerIndex == playerIndex)
                return d;
        }
        return null;
    }

    /// <summary>
    /// Returns the connected gamepad with the given id, or null if not present.
    /// </summary>
    public static GamepadDevice? ById(GamepadId id)
    {
        foreach (var d in Devices)
        {
            if (d.Id == id)
                return d;
        }
        return null;
    }

    /// <summary>
    /// Closes all cached gamepad devices. The next access to <see cref="Devices"/>
    /// will reopen them. Useful if device state appears stale.
    /// </summary>
    public static void Reset()
    {
        ImmutableDictionary<uint, GamepadDevice> snapshot;
        lock (_sync)
        {
            snapshot = _cache;
            _cache = ImmutableDictionary<uint, GamepadDevice>.Empty;
        }
        foreach (var d in snapshot.Values)
            d.Dispose();
    }

    internal static void Forget(GamepadId id)
    {
        lock (_sync)
        {
            _cache = _cache.Remove(id.Value);
        }
    }

    private static IReadOnlyList<GamepadDevice> Refresh()
    {
        var ids = SDL.GetGamepads(out var count);
        if (ids == null || count == 0)
        {
            // Drop any cached devices that no longer correspond to live ones.
            PruneCache(System.Array.Empty<uint>());
            return System.Array.Empty<GamepadDevice>();
        }

        var live = new uint[count];
        System.Array.Copy(ids, live, count);

        PruneCache(live);

        var result = new List<GamepadDevice>(count);
        foreach (var id in live)
        {
            var device = GetOrOpen(id);
            if (device != null)
                result.Add(device);
        }
        return result;
    }

    private static GamepadDevice? GetOrOpen(uint id)
    {
        lock (_sync)
        {
            if (_cache.TryGetValue(id, out var existing))
                return existing;
        }

        var handle = SDL.OpenGamepad(id);
        if (handle == 0)
            return null;

        var device = new GamepadDevice(handle, new GamepadId(id));
        lock (_sync)
        {
            // Another thread may have raced us; if so, close ours and use theirs.
            if (_cache.TryGetValue(id, out var existing))
            {
                SDL.CloseGamepad(handle);
                return existing;
            }
            _cache = _cache.Add(id, device);
        }
        return device;
    }

    private static void PruneCache(uint[] live)
    {
        ImmutableDictionary<uint, GamepadDevice> oldCache;
        ImmutableDictionary<uint, GamepadDevice>.Builder? toRemove = null;

        lock (_sync)
        {
            if (_cache.IsEmpty)
                return;

            oldCache = _cache;
            var liveSet = new HashSet<uint>(live);
            foreach (var kvp in oldCache)
            {
                if (!liveSet.Contains(kvp.Key))
                {
                    toRemove ??= ImmutableDictionary.CreateBuilder<uint, GamepadDevice>();
                    toRemove[kvp.Key] = kvp.Value;
                }
            }
            if (toRemove != null)
                _cache = _cache.RemoveRange(toRemove.Keys);
        }

        if (toRemove != null)
        {
            foreach (var d in toRemove.Values)
                d.Dispose();
        }
    }

    private static void EnsureInit()
    {
        _ = Application.Current;
        if (_initialized)
            return;
        lock (_sync)
        {
            if (_initialized)
                return;
            if (!SDL.InitSubSystem(SDL.InitFlags.Gamepad))
                throw new InvalidOperationException(
                    $"Failed to initialize the SDL Gamepad subsystem: {SDL.GetError()}");
            _initialized = true;
        }
    }
}

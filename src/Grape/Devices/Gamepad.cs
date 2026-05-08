using System.Collections.Immutable;

namespace Grape.Devices;

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

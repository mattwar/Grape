using System.Collections.Immutable;

using Blitter.Devices;

namespace Blitter;

public static class Audio
{
    /// <summary>
    /// Ensures the application is running and the SDL audio subsystem is
    /// initialized. Safe to call from any audio entry point; SDL ref-counts
    /// subsystem init so repeated calls are cheap.
    /// </summary>
    private static void EnsureInit()
    {
        _ = Application.Current;
        if (!SDL.InitSubSystem(SDL.InitFlags.Audio))
            throw new InvalidOperationException(
                $"Failed to initialize SDL audio subsystem: {SDL.GetError()}");
    }

    /// <summary>
    /// Plays the audio data on the default playback device.
    /// </summary>
    public static void Play(Sound data, float volume = 1f)
    {
        // fire and forget
        var _ = PlayAsync(data, volume);
    }
    
    /// <summary>
    /// Plays the audio data on the default playback device.
    /// </summary>
    public static Task PlayAsync(Sound data, float volume = 1f)
    {
        EnsureInit();
        return AudioPlaybackDevice.Default.PlayAsync(data, volume);
    }

    private static ImmutableList<AudioPlaybackDevice>? _playbackDevices;
    private static ImmutableList<AudioRecordingDevice>? _recordingDevices;
    private static ImmutableList<string>? _driverNames;

    public static AudioPlaybackDevice DefaultPlaybackDevice =>
        Audio.PlaybackDevices.Count > 0 
            ? Audio.PlaybackDevices[0] 
            : throw new InvalidOperationException("No playback devices available.");

    public static AudioRecordingDevice DefaultRecordingDevice =>
        Audio.RecordingDevices.Count > 0 
            ? Audio.RecordingDevices[0] 
            : throw new InvalidOperationException("No recording devices available.");

    /// <summary>
    /// The set of available audio playback devices.
    /// </summary>
    public static ImmutableList<AudioPlaybackDevice> PlaybackDevices
    {
        get
        {
            EnsureInit();
            var devices = _playbackDevices;
            if (devices == null)
            {
                var ids = SDL.GetAudioPlaybackDevices(out var count);
                if (ids == null)
                    throw new InvalidOperationException("Unable to get audio playback devices.");

                if (count > 0)
                {
                    devices = ids.Select(id => new AudioPlaybackDevice(id)).ToImmutableList();
                }
                else
                {
                    devices = ImmutableList<AudioPlaybackDevice>.Empty;
                }

                Interlocked.CompareExchange(ref _playbackDevices, devices, null);
            }
            return _playbackDevices!;
        }
    }

    /// <summary>
    /// The set of available audio recording devices.
    /// </summary>
    public static ImmutableList<AudioRecordingDevice> RecordingDevices
    {
        get
        {
            EnsureInit();
            var devices = _recordingDevices;
            if (devices == null)
            {
                var ids = SDL.GetAudioRecordingDevices(out var count);
                if (ids != null && count > 0)
                {
                    devices = ids.Select(id => new AudioRecordingDevice(id)).ToImmutableList();
                }
                else
                {
                    devices = ImmutableList<AudioRecordingDevice>.Empty;
                }
                Interlocked.CompareExchange(ref _recordingDevices, devices, null);
            }
            return _recordingDevices!;
        }
    }

    /// <summary>
    /// The name of the current audio driver.
    /// </summary>
    public static string CurrentDriver
    {
        get
        {
            EnsureInit();
            return SDL.GetCurrentAudioDriver() ?? "";
        }
    }

    /// <summary>
    /// The names of all built-in audio drivers.
    /// </summary>
    public static ImmutableList<string> Drivers
    {
        get
        {
            EnsureInit();
            var driverNames = _driverNames;
            if (driverNames == null)
            {
                if (SDL.GetNumAudioDrivers() is { } count
                    && count > 0)
                {
                    driverNames = Enumerable.Range(0, count)
                        .Select(i => SDL.GetAudioDriver(i))
                        .Where(name => name != null)
                        .ToImmutableList()!;
                }
                else
                {
                    driverNames = ImmutableList<string>.Empty;
                }
                Interlocked.CompareExchange(ref _driverNames, driverNames, null);
            }
            return _driverNames!;
        }
    }
}

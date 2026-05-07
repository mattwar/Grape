using System.Collections.Immutable;

namespace Grape;

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
    public static void Play(AudioData data, float volume = 1f)
    {
        // fire and forget
        var _ = PlayAsync(data, volume);
    }
    
    /// <summary>
    /// Plays the audio data on the default playback device.
    /// </summary>
    public static Task PlayAsync(AudioData data, float volume = 1f)
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

/// <summary>
/// The base class for audio devices, either playback or recording.
/// </summary>
public abstract class AudioDevice
{
    private protected uint _deviceId;

    internal AudioDevice(uint deviceId)
    {
        _deviceId = deviceId;
    }

    /// <summary>
    /// The name of the audio device.
    /// </summary>
    public string Name =>
        SDL.GetAudioDeviceName(_deviceId) ?? "";

    /// <summary>
    /// The specifications of the audio device.
    /// </summary>
    public AudioSpec Spec =>
        _deviceId != 0 && SDL.GetAudioDeviceFormat(_deviceId, out var spec, out _)
            ? AudioSpec.From(spec)
            : default;

    /// <summary>
    /// The number of sample frames in the audio device's buffer.
    /// </summary>
    public int SampleFrames =>
        _deviceId != 0 && SDL.GetAudioDeviceFormat(_deviceId, out _, out var sampleFrames)
            ? sampleFrames
            : 0;

    /// <summary>
    /// The volume of the audio device, from 0.0 (silent) to 1.0 (full volume).
    /// </summary>
    public virtual float Volume
    {
        get => -1f;
        set { }
    }
}

/// <summary>
/// An audio playback device that can play audio streams.
/// </summary>
public class AudioPlaybackDevice : AudioDevice
{
    internal AudioPlaybackDevice(uint deviceId)
        : base(deviceId)
    {
    }

    /// <summary>
    /// Opens the default playback device with the specified audio specifications.
    /// </summary>
    public static AudioPlaybackDevice Default => Audio.DefaultPlaybackDevice;

    /// <summary>
    /// Open the audio device for playback or recording.
    /// </summary>
    public LogicalPlaybackDevice Open()
    {
        var id = SDL.OpenAudioDevice(_deviceId, this.Spec.ToSdl());
        if (id == 0)
            throw new InvalidOperationException($"SDL_OpenAudioDevice Error: {SDL.GetError()}");
        return new LogicalPlaybackDevice(id);
    }

    /// <summary>
    /// Plays the audio data on the device.
    /// </summary>
    public void Play(AudioData data, float volume = 1f)
    {
        // fire and forget
        var _ = PlayAsync(data, volume);
    }

    /// <summary>
    /// Plays the audio data on the device.
    /// </summary>
    public virtual async Task PlayAsync(AudioData data, float volume = 1f)
    {
        var device = Open();
        await device.PlayAsync(data, volume);
        device.Dispose();
    }
}

public class AudioRecordingDevice : AudioDevice
{
    internal AudioRecordingDevice(uint deviceId)
        : base(deviceId)
    {
    }

    /// <summary>
    /// Opens the default recording device with the specified audio specifications.
    /// </summary>
    public static AudioRecordingDevice Default => Audio.DefaultRecordingDevice;
}

/// <summary>
/// An opened audio device that can play audio streams.
/// </summary>
public class LogicalPlaybackDevice : AudioPlaybackDevice
{
    private ImmutableList<AudioStream> _streams = ImmutableList<AudioStream>.Empty;

    internal LogicalPlaybackDevice(uint deviceId)
        : base(deviceId)
    {
    }

    public bool IsDisposed => _deviceId == 0;

    public void Dispose()
    {
        if (!IsDisposed)
        {
            var id = Interlocked.Exchange(ref _deviceId, 0);
            if (id != 0)
            {
                foreach (var stream in _streams)
                {
                    stream.Dispose();
                }

                SDL.CloseAudioDevice(id);
            }
        }
    }

    /// <summary>
    /// The volume of the audio device, from 0.0 (silent) to 1.0 (full volume).
    /// </summary>
    public override float Volume
    {
        get => SDL.GetAudioDeviceGain(_deviceId);
        set => SDL.SetAudioDeviceGain(_deviceId, value);
    }

    /// <summary>
    /// Play the specified audio data on the device.
    /// </summary>
    public override Task PlayAsync(AudioData data, float volume = 1f)
    {
        this.Volume = volume;

        var tcs = new TaskCompletionSource();
        var stream = CreateStream(data.Spec, (stream, additionalAmount, totalAmount) =>
        {
            if (stream.IsDisposed)
            {
                if (!tcs.Task.IsCompleted)
                {
                    tcs.SetResult();
                }
                return;
            }

            if (stream.QueuedBytes == 0)
            {
                if (tcs.Task.IsCompleted)
                    return;
                Task.Run(() =>
                {
                    stream.Dispose();
                    tcs.SetResult();
                });
            }
        });

        stream.Queue(data);
        stream.Paused = false;
        return tcs.Task;
    }

    #region Audio Streams

    /// <summary>
    /// The set of current audio streams.
    /// </summary>
    public ImmutableList<AudioStream> Streams => _streams;

    public AudioStream CreateStream(AudioSpec sourceSpec, AudioDataRequested? onDataRequested = null)
    {
        var streamId = SDL.CreateAudioStream(sourceSpec.ToSdl(), this.Spec.ToSdl());
        if (streamId == 0)
            throw new InvalidOperationException($"SDL_OpenAudioDeviceStream Error: {SDL.GetError()}");

        if (!SDL.BindAudioStream(_deviceId, streamId))
            throw new InvalidOperationException($"SDL_BindAudioStream Error: {SDL.GetError()}");

        return new AudioStream(this, streamId, onDataRequested);
    }

    internal void AddStream(AudioStream stream)
    {
        ImmutableInterlocked.Update(ref _streams, (list) => list.Add(stream));
    }

    internal void RemoveStream(AudioStream stream)
    {
        ImmutableInterlocked.Update(ref _streams, (list) => list.Remove(stream));
    }

    #endregion
}

public delegate void AudioDataRequested(AudioStream stream, int additionalAmount, int totalAmount);

public class AudioStream : IDisposable
{
    private LogicalPlaybackDevice _device;
    private nint _streamId;
    private readonly AudioDataRequested? _onDataRequested;

    internal AudioStream(LogicalPlaybackDevice device, nint streamId, AudioDataRequested? onDataRequested = null)
    {
        _device = device;
        _streamId = streamId;
        _onDataRequested = onDataRequested;

        // use callback to maybe determine when nothing is left in the queue
        SDL.SetAudioStreamGetCallback(_streamId, GetDataCallback, nint.Zero);
    }

    private void GetDataCallback(nint userdata, nint stream, int additionalAmount, int totalAmount)
    {
        _onDataRequested?.Invoke(this, additionalAmount, totalAmount);
    }

    public bool IsDisposed => _streamId == 0;

    public void Dispose()
    {
        if (!IsDisposed)
        {
            var id = Interlocked.Exchange(ref _streamId, 0);
            if (id != 0)
            {
                _device.RemoveStream(this);
                _device = null!;
                SDL.DestroyAudioStream(id);
            }
        }
    }

    public float Volume
    {
        get
        {
            return IsDisposed 
                ? 0f
                : SDL.GetAudioStreamGain(_streamId);
        }
        set
        {
            if (value < 0.0f || value > 1.0f)
                throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0.0 and 1.0");
            if (!IsDisposed)
            {
                SDL.SetAudioStreamGain(_streamId, value);
            }
        }
    }

    public int QueuedBytes
    {
        get
        {
            return IsDisposed
                ? 0
                : SDL.GetAudioStreamQueued(_streamId);
        }
    }

    /// <summary>
    /// True if the stream is paused.
    /// </summary>
    public bool Paused
    {
        get
        {
            return IsDisposed
                ? true
                : SDL.AudioStreamDevicePaused(_streamId);
        }
        set
        {
            if (IsDisposed)
                return;
            if (value)
                SDL.PauseAudioStreamDevice(_streamId);
            else
                SDL.ResumeAudioStreamDevice(_streamId);
        }
    }

    /// <summary>
    /// Clears any queued audio data in the stream.
    /// </summary>
    /// <exception cref="ObjectDisposedException"></exception>
    public void Clear()
    {
        if (IsDisposed)
            return;
        SDL.ClearAudioStream(_streamId);
    }

    /// <summary>
    /// Flushes any queued audio data to the device (aka, play it now).
    /// </summary>
    public void Flush()
    {
        if (IsDisposed)
            return;
        SDL.FlushAudioStream(_streamId);
    }

    /// <summary>
    /// Queues audio data to be played on the stream.
    /// </summary>
    public void Queue(AudioData data)
    {
        unsafe
        {
            var span = data.Data.Span;
            fixed (byte* pData = span)
            {
                SDL.PutAudioStreamData(_streamId, (nint)pData, span.Length);
            }
        }
    }
}

/// <summary>
/// Represents audio data, including its specification and raw audio bytes.
/// </summary>
public sealed class AudioData
{
    public AudioSpec Spec { get; }
    public ReadOnlyMemory<byte> Data { get; }

    public AudioData(AudioSpec spec, ReadOnlyMemory<byte> data)
    {
        this.Spec = spec;
        this.Data = data;
    }

    /// <summary>
    /// Loads the WAV file from the specified path.
    /// </summary>
    public static AudioData LoadWAV(string path)
    {
        if (!SDL.LoadWAV(path, out var spec, out var audioBuffer, out var audioLength))
            throw new InvalidOperationException($"SDL_LoadWAV Error: {SDL.GetError()}");
        unsafe
        {
            byte* sourceBytesPtr = (byte*)audioBuffer;
            var bytes = new byte[audioLength];
            fixed (byte* targetBytePtr = bytes)
            {
                Buffer.MemoryCopy(sourceBytesPtr, targetBytePtr, audioLength, audioLength);
            }
            var data = new ReadOnlyMemory<byte>(bytes);
            return new AudioData(AudioSpec.From(spec), data);
        }
    }
}

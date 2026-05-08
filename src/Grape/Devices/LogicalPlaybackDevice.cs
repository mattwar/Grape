using System.Collections.Immutable;

namespace Grape.Devices;

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
    public override Task PlayAsync(Sound data, float volume = 1f)
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

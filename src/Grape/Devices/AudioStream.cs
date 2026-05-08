namespace Grape.Devices;

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
    public void Queue(Sound data)
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

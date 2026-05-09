namespace Blitter.Devices;

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
    public void Play(Sound data, float volume = 1f)
    {
        // fire and forget
        var _ = PlayAsync(data, volume);
    }

    /// <summary>
    /// Plays the audio data on the device.
    /// </summary>
    public virtual async Task PlayAsync(Sound data, float volume = 1f)
    {
        var device = Open();
        await device.PlayAsync(data, volume);
        device.Dispose();
    }
}

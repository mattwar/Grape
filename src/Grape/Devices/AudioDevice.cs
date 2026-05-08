namespace Grape.Devices;

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

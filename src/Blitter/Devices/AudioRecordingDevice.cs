namespace Blitter.Devices;

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

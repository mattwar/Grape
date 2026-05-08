namespace Grape.Devices;

/// <summary>
/// Describes the binary layout of a stream of audio samples: sample format,
/// channel count, and sample rate (in Hertz).
/// </summary>
public readonly record struct AudioSpec
{
    /// <summary>The sample format.</summary>
    public AudioFormat Format { get; init; }

    /// <summary>Number of channels (1 = mono, 2 = stereo, etc.).</summary>
    public int Channels { get; init; }

    /// <summary>Sample rate in Hertz.</summary>
    public int Frequency { get; init; }

    public AudioSpec(AudioFormat format, int channels, int frequency)
    {
        this.Format = format;
        this.Channels = channels;
        this.Frequency = frequency;
    }

    internal SDL.AudioSpec ToSdl() => new()
    {
        Format = (SDL.AudioFormat)Format,
        Channels = Channels,
        Freq = Frequency,
    };

    internal static AudioSpec From(SDL.AudioSpec spec) => new()
    {
        Format = (AudioFormat)spec.Format,
        Channels = spec.Channels,
        Frequency = spec.Freq,
    };
}

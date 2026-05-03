namespace Grape;

/// <summary>
/// The sample format of audio data.
/// </summary>
/// <remarks>
/// Values mirror the underlying <c>SDL_AudioFormat</c> bit pattern so they
/// can be cast at the SDL boundary.
/// </remarks>
public enum AudioFormat : uint
{
    /// <summary>Unknown / unspecified format.</summary>
    Unknown = 0x00000000,

    /// <summary>Unsigned 8-bit samples.</summary>
    U8 = 0x00000008,

    /// <summary>Signed 8-bit samples.</summary>
    S8 = 0x00008008,

    /// <summary>Signed 16-bit samples, little endian.</summary>
    S16LE = 0x00008010,

    /// <summary>Signed 32-bit samples, little endian.</summary>
    S32LE = 0x00008020,

    /// <summary>32-bit floating point samples, little endian.</summary>
    F32LE = 0x00008120,

    /// <summary>Signed 16-bit samples, big endian.</summary>
    S16BE = 0x00009010,

    /// <summary>Signed 32-bit samples, big endian.</summary>
    S32BE = 0x00009020,

    /// <summary>32-bit floating point samples, big endian.</summary>
    F32BE = 0x00009120,
}

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

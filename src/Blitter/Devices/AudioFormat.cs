namespace Blitter.Devices;

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

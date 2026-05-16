namespace Blitter;

/// <summary>
/// Basic oscillator shapes used by <see cref="Sound.Tone"/> and
/// <see cref="Sound.Sweep"/>.
/// </summary>
public enum Waveform
{
    /// <summary>Pure sine wave; smooth, flute-like.</summary>
    Sine,

    /// <summary>Square / pulse wave; classic 8-bit lead.</summary>
    Square,

    /// <summary>Triangle wave; mellow, NES bass channel.</summary>
    Triangle,

    /// <summary>Sawtooth wave; bright and buzzy.</summary>
    Sawtooth,

    /// <summary>White noise; drums, explosions, hiss.</summary>
    Noise,

    /// <summary>Brown (red) noise; low-frequency rumble, waterfalls, rocket roar.</summary>
    BrownNoise,
}

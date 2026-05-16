using System.Buffers.Binary;

using Blitter.Devices;

namespace Blitter.Tests;

public class SoundSynthTests
{
    private const int SampleRate = 44100;

    private static ReadOnlySpan<byte> Bytes(Sound s) => s.Data.Span;

    private static float SampleAt(Sound s, int i)
    {
        var span = s.Data.Span.Slice(i * sizeof(float), sizeof(float));
        return BinaryPrimitives.ReadSingleLittleEndian(span);
    }

    private static int SampleCount(Sound s) => s.Data.Length / sizeof(float);

    [Fact]
    public void Tone_HasExpectedSpecAndLength()
    {
        var s = Sound.Tone(440f, 0.25f);

        Assert.Equal(AudioFormat.F32LE, s.Spec.Format);
        Assert.Equal(1, s.Spec.Channels);
        Assert.Equal(SampleRate, s.Spec.Frequency);
        Assert.Equal((int)MathF.Round(0.25f * SampleRate), SampleCount(s));
    }

    [Fact]
    public void Tone_AppliesAttackAndReleaseEnvelope()
    {
        var s = Sound.Tone(440f, 0.5f, Waveform.Sine, volume: 1f,
            attack: 0.01f, release: 0.05f);

        // First sample is silent (start of linear attack).
        Assert.Equal(0f, SampleAt(s, 0));
        // Last sample is silent (end of linear release).
        Assert.Equal(0f, SampleAt(s, SampleCount(s) - 1));
    }

    [Fact]
    public void Tone_ZeroDuration_ReturnsEmptyBuffer()
    {
        var s = Sound.Tone(440f, 0f);
        Assert.Equal(0, s.Data.Length);
        Assert.Equal(AudioFormat.F32LE, s.Spec.Format);
    }

    [Fact]
    public void Tone_RespectsVolume()
    {
        var s = Sound.Tone(440f, 0.2f, Waveform.Square, volume: 0.25f,
            attack: 0.001f, release: 0.001f);

        float peak = 0f;
        int count = SampleCount(s);
        for (int i = 0; i < count; i++)
            peak = MathF.Max(peak, MathF.Abs(SampleAt(s, i)));

        Assert.True(peak <= 0.2501f, $"peak {peak} exceeded volume 0.25");
        Assert.True(peak >= 0.2f, $"peak {peak} unexpectedly low");
    }

    [Fact]
    public void Tone_SquareIsBipolar()
    {
        var s = Sound.Tone(200f, 0.1f, Waveform.Square, volume: 1f,
            attack: 0.001f, release: 0.001f);

        bool sawPositive = false, sawNegative = false;
        int count = SampleCount(s);
        for (int i = 0; i < count; i++)
        {
            float v = SampleAt(s, i);
            if (v > 0.5f) sawPositive = true;
            if (v < -0.5f) sawNegative = true;
        }
        Assert.True(sawPositive && sawNegative);
    }

    [Fact]
    public void Tone_IsDeterministic()
    {
        var a = Sound.Tone(440f, 0.1f, Waveform.Noise);
        var b = Sound.Tone(440f, 0.1f, Waveform.Noise);
        Assert.True(Bytes(a).SequenceEqual(Bytes(b)));
    }

    [Fact]
    public void Sweep_ProducesSameLengthAsTone()
    {
        var a = Sound.Sweep(880f, 110f, 0.3f);
        var b = Sound.Tone(440f, 0.3f);
        Assert.Equal(SampleCount(b), SampleCount(a));
    }

    [Fact]
    public void Sounds_Presets_ReturnNonEmptyF32LeMono()
    {
        Sound[] presets =
        {
            Sounds.Blip,
            Sounds.Select,
            Sounds.Coin,
            Sounds.Jump,
            Sounds.Laser,
            Sounds.Explosion,
            Sounds.Hurt,
            Sounds.PowerUp,
            Sounds.Siren,
            Sounds.Klaxon,
            Sounds.Warble,
            Sounds.CreateSiren(0.4f),
            Sounds.CreateKlaxon(0.6f),
            Sounds.CreateWarble(0.3f),
        };

        foreach (var s in presets)
        {
            Assert.Equal(AudioFormat.F32LE, s.Spec.Format);
            Assert.Equal(1, s.Spec.Channels);
            Assert.Equal(SampleRate, s.Spec.Frequency);
            Assert.True(s.Data.Length > 0);
            Assert.Equal(0, s.Data.Length % sizeof(float));
        }
    }

    [Fact]
    public void Sounds_Coin_LengthMatchesConcatenatedParts()
    {
        var coin = Sounds.Coin;
        int expected = (int)MathF.Round(0.08f * SampleRate)
                     + (int)MathF.Round(0.32f * SampleRate);
        Assert.Equal(expected, SampleCount(coin));
    }

    [Fact]
    public void Sounds_CachedProperty_ReturnsSameInstance()
    {
        // Properties hand back the cached buffer, not a fresh synthesis.
        Assert.Same(Sounds.Coin, Sounds.Coin);
    }
}

using Blitter.Bits;
using Blitter.Devices;

namespace Blitter.Tests;

public class MelodyTests
{
    [Fact]
    public void CreateMelody_ProducesF32LeMono44100()
    {
        var sound = Sound.CreateMelody("c d e");
        Assert.Equal(AudioFormat.F32LE, sound.Spec.Format);
        Assert.Equal(1, sound.Spec.Channels);
        Assert.Equal(44100, sound.Spec.Frequency);
        Assert.True(sound.Data.Length > 0);
    }

    [Fact]
    public void CreateMelody_LengthMatchesBeatsAtBpm()
    {
        // 4 beats at 120 bpm = 2 seconds = 88200 samples = 352800 bytes (F32).
        var sound = Sound.CreateMelody("c d e f", bpm: 120);
        Assert.Equal(4 * 44100 * sizeof(float) / 2, sound.Data.Length); // 4 beats * 0.5 s/beat * rate * bytes
    }

    [Fact]
    public void CreateMelody_RestsHaveZeroAmplitude()
    {
        // Two beats of rest at 120 bpm = 1 s of silence at the start.
        var sound = Sound.CreateMelody("r:2 c", bpm: 120);
        var span = sound.Data.Span;
        // Sample at 0.5 s mark — well inside the rest.
        int byteOffset = (int)(0.5f * 44100) * sizeof(float);
        float v = System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(
            span.Slice(byteOffset, sizeof(float)));
        Assert.Equal(0f, v);
    }

    [Fact]
    public void CreateMelody_BarLinesAreIgnored()
    {
        var withBars = Sound.CreateMelody("c d | e f");
        var withoutBars = Sound.CreateMelody("c d e f");
        Assert.Equal(withoutBars.Data.Length, withBars.Data.Length);
    }

    [Fact]
    public void CreateMelody_DefaultOctaveApplies()
    {
        // Same note with explicit octave matches default.
        var a = Sound.CreateMelody("c", defaultOctave: 5);
        var b = Sound.CreateMelody("c5", defaultOctave: 4);
        Assert.Equal(a.Data.Length, b.Data.Length);
        Assert.True(a.Data.Span.SequenceEqual(b.Data.Span));
    }

    [Theory]
    [InlineData("z")]       // bad letter
    [InlineData("c:0")]     // zero duration
    [InlineData("c:abc")]   // bad duration
    [InlineData("c5x")]     // bad octave
    public void CreateMelody_InvalidTokenThrows(string score)
    {
        Assert.Throws<FormatException>(() => Sound.CreateMelody(score));
    }
}

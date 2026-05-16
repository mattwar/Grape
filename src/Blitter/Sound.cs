using System.Buffers.Binary;

using Blitter.Devices;

using NLayer;

using NVorbis;

namespace Blitter;

/// <summary>
/// A loaded sound: an <see cref="AudioSpec"/> describing the format
/// plus the raw PCM bytes. Pass to <see cref="Audio.Play(Sound, float)"/>
/// or <see cref="AudioPlaybackDevice.Play(Sound, float)"/>.
/// </summary>
public sealed class Sound
{
    public AudioSpec Spec { get; }
    public ReadOnlyMemory<byte> Data { get; }

    public Sound(AudioSpec spec, ReadOnlyMemory<byte> data)
    {
        this.Spec = spec;
        this.Data = data;
    }

    /// <summary>
    /// Loads a sound from disk. Format is determined by the file extension:
    /// <c>.wav</c>, <c>.ogg</c>, <c>.mp3</c>.
    /// </summary>
    public static Sound Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".wav" => LoadWav(path),
            ".ogg" or ".oga" => LoadOgg(File.OpenRead(path), closeStream: true),
            ".mp3" => LoadMp3(File.OpenRead(path), closeStream: true),
            _ => throw new NotSupportedException($"Unsupported audio file extension: '{ext}'."),
        };
    }

    /// <summary>
    /// Loads a sound from a stream. Format is determined by sniffing the
    /// first few bytes (WAV / OGG / MP3). The stream is not closed.
    /// </summary>
    public static Sound Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Decode(stream);
    }

    private static Sound Decode(Stream stream)
    {
        // Sniff magic bytes. WAV = "RIFF", OGG = "OggS", MP3 = "ID3" or 0xFF 0xFB/0xFA/0xF3/0xF2 frame sync.
        Span<byte> magic = stackalloc byte[4];
        if (!stream.CanSeek)
            throw new NotSupportedException("Decode requires a seekable stream; wrap non-seekable input in a MemoryStream first.");
        long origin = stream.Position;
        int read = stream.Read(magic);
        stream.Position = origin;
        if (read < 4)
            throw new InvalidDataException("Stream too short to identify audio format.");

        if (magic[0] == 'R' && magic[1] == 'I' && magic[2] == 'F' && magic[3] == 'F')
            return LoadWavFromStream(stream);
        if (magic[0] == 'O' && magic[1] == 'g' && magic[2] == 'g' && magic[3] == 'S')
            return LoadOgg(stream, closeStream: false);
        if ((magic[0] == 'I' && magic[1] == 'D' && magic[2] == '3') ||
            (magic[0] == 0xFF && (magic[1] & 0xE0) == 0xE0))
            return LoadMp3(stream, closeStream: false);

        throw new InvalidDataException("Unrecognized audio format (expected WAV, OGG, or MP3).");
    }

    private static Sound LoadWav(string path)
    {
        if (!SDL.LoadWAV(path, out var spec, out var audioBuffer, out var audioLength))
            throw new InvalidOperationException($"SDL_LoadWAV Error: {SDL.GetError()}");
        unsafe
        {
            byte* sourceBytesPtr = (byte*)audioBuffer;
            var bytes = new byte[audioLength];
            fixed (byte* targetBytePtr = bytes)
            {
                Buffer.MemoryCopy(sourceBytesPtr, targetBytePtr, audioLength, audioLength);
            }
            return new Sound(AudioSpec.From(spec), new ReadOnlyMemory<byte>(bytes));
        }
    }

    private static Sound LoadWavFromStream(Stream stream)
    {
        // SDL_LoadWAV only takes a path, so buffer to a temp file. Cheap for
        // short SFX; callers wanting zero-copy WAV from streams can swap in
        // a managed RIFF parser later.
        var temp = Path.Combine(Path.GetTempPath(), $"blitter-{Guid.NewGuid():N}.wav");
        try
        {
            using (var fs = File.Create(temp))
                stream.CopyTo(fs);
            return LoadWav(temp);
        }
        finally
        {
            try { File.Delete(temp); } catch { }
        }
    }

    private static Sound LoadOgg(Stream stream, bool closeStream)
    {
        using var reader = new VorbisReader(stream, closeStream);
        return ReadAllFloatSamples(reader.Channels, reader.SampleRate, reader.ReadSamples);
    }

    private static Sound LoadMp3(Stream stream, bool closeStream)
    {
        using var mpeg = new MpegFile(stream);
        try
        {
            return ReadAllFloatSamples(mpeg.Channels, mpeg.SampleRate, mpeg.ReadSamples);
        }
        finally
        {
            if (closeStream)
                stream.Dispose();
        }
    }

    private static Sound ReadAllFloatSamples(int channels, int sampleRate, Func<float[], int, int, int> readSamples)
    {
        // Stream the decoder in fixed-size chunks; grow a managed buffer
        // until ReadSamples reports EOF (return value < requested).
        const int chunkFloats = 16384;
        var floats = new float[chunkFloats];
        var samples = new List<float>(chunkFloats * 4);
        int got;
        while ((got = readSamples(floats, 0, chunkFloats)) > 0)
        {
            samples.AddRange(new ReadOnlySpan<float>(floats, 0, got));
            if (got < chunkFloats)
                break;
        }

        var bytes = new byte[samples.Count * sizeof(float)];
        var dst = bytes.AsSpan();
        for (int i = 0; i < samples.Count; i++)
            BinaryPrimitives.WriteSingleLittleEndian(dst.Slice(i * sizeof(float), sizeof(float)), samples[i]);

        var spec = new AudioSpec(AudioFormat.F32LE, channels, sampleRate);
        return new Sound(spec, bytes);
    }

    // Sample rate used by the procedural synth helpers (Tone, Sweep).
    private const int SynthSampleRate = 44100;

    /// <summary>
    /// Returns a new sound consisting of <paramref name="sound"/> plus
    /// <paramref name="repeats"/> progressively quieter copies of it, each
    /// offset by <paramref name="delay"/> seconds. Useful for echo / reverb
    /// fakes, "voice in a cave" effects and beefier explosions. Output
    /// length grows by <c>delay × repeats</c>. F32LE only.
    /// </summary>
    /// <param name="sound">Input sound. Must be F32LE.</param>
    /// <param name="delay">Time between repeats, in seconds.</param>
    /// <param name="decay">Volume multiplier applied per tap (0..1). 0.5 = each echo half as loud as the previous.</param>
    /// <param name="repeats">Number of echo taps after the original.</param>
    public static Sound Echo(Sound sound, float delay = 0.15f, float decay = 0.5f, int repeats = 3)
    {
        ArgumentNullException.ThrowIfNull(sound);
        if (sound.Spec.Format != AudioFormat.F32LE)
            throw new NotSupportedException("Sound.Echo currently supports F32LE only.");
        if (repeats < 0)
            throw new ArgumentOutOfRangeException(nameof(repeats));

        int rate = sound.Spec.Frequency;
        int srcSamples = sound.Data.Length / sizeof(float);
        int delaySamples = Math.Max(1, (int)MathF.Round(delay * rate));
        int totalSamples = srcSamples + delaySamples * repeats;
        if (totalSamples == 0)
            return new Sound(sound.Spec, ReadOnlyMemory<byte>.Empty);

        var srcSpan = sound.Data.Span;
        var accum = new float[totalSamples];

        // Original + each tap reads from the source (clean repeats, not
        // feedback). Cheaper, and easier to reason about.
        for (int r = 0; r <= repeats; r++)
        {
            int offset = r * delaySamples;
            float gain = (float)Math.Pow(decay, r);
            for (int i = 0; i < srcSamples; i++)
            {
                float v = BinaryPrimitives.ReadSingleLittleEndian(srcSpan.Slice(i * sizeof(float), sizeof(float)));
                accum[i + offset] += v * gain;
            }
        }

        var buffer = new byte[totalSamples * sizeof(float)];
        var outSpan = buffer.AsSpan();
        for (int i = 0; i < totalSamples; i++)
        {
            float v = accum[i];
            if (v > 1f) v = 1f;
            else if (v < -1f) v = -1f;
            BinaryPrimitives.WriteSingleLittleEndian(outSpan.Slice(i * sizeof(float), sizeof(float)), v);
        }
        return new Sound(sound.Spec, buffer);
    }

    /// <summary>
    /// Concatenates sounds in order. All inputs must share the same
    /// <see cref="AudioSpec"/>. Useful for arpeggios and melodies.
    /// </summary>
    public static Sound Concat(params Sound[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Length == 0)
            return new Sound(new AudioSpec(AudioFormat.F32LE, 1, SynthSampleRate), ReadOnlyMemory<byte>.Empty);
        if (parts.Length == 1)
            return parts[0];

        var spec = parts[0].Spec;
        int total = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Spec != spec)
                throw new ArgumentException("All sounds must share the same AudioSpec to concat.", nameof(parts));
            total += parts[i].Data.Length;
        }

        var buffer = new byte[total];
        int offset = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            var src = parts[i].Data.Span;
            src.CopyTo(buffer.AsSpan(offset));
            offset += src.Length;
        }
        return new Sound(spec, buffer);
    }

    /// <summary>
    /// Sums two or more sounds sample-by-sample (all must share the same
    /// <see cref="AudioSpec"/>). The output length is the longest input;
    /// samples are clipped to [-1, 1]. Currently supports
    /// <see cref="AudioFormat.F32LE"/>.
    /// </summary>
    public static Sound Mix(params Sound[] sounds)
    {
        ArgumentNullException.ThrowIfNull(sounds);
        if (sounds.Length == 0)
            return new Sound(new AudioSpec(AudioFormat.F32LE, 1, SynthSampleRate), ReadOnlyMemory<byte>.Empty);
        if (sounds.Length == 1)
            return sounds[0];

        var spec = sounds[0].Spec;
        if (spec.Format != AudioFormat.F32LE)
            throw new NotSupportedException("Sound.Mix currently supports F32LE only.");

        int maxBytes = 0;
        for (int i = 0; i < sounds.Length; i++)
        {
            if (sounds[i].Spec != spec)
                throw new ArgumentException("All sounds must share the same AudioSpec to mix.", nameof(sounds));
            if (sounds[i].Data.Length > maxBytes)
                maxBytes = sounds[i].Data.Length;
        }

        int maxSamples = maxBytes / sizeof(float);
        var buffer = new byte[maxSamples * sizeof(float)];
        var outSpan = buffer.AsSpan();

        for (int i = 0; i < maxSamples; i++)
        {
            double sum = 0;
            for (int s = 0; s < sounds.Length; s++)
            {
                var span = sounds[s].Data.Span;
                int byteOffset = i * sizeof(float);
                if (byteOffset + sizeof(float) <= span.Length)
                    sum += BinaryPrimitives.ReadSingleLittleEndian(span.Slice(byteOffset, sizeof(float)));
            }
            if (sum > 1.0) sum = 1.0;
            else if (sum < -1.0) sum = -1.0;
            BinaryPrimitives.WriteSingleLittleEndian(outSpan.Slice(i * sizeof(float), sizeof(float)), (float)sum);
        }

        return new Sound(spec, buffer);
    }

    /// <summary>
    /// One-pole low-pass filter. Smooths sharp edges (square/saw clicks) and
    /// rolls off high frequencies above <paramref name="cutoffHz"/>, turning
    /// buzzy waveforms into rumbles. Currently supports
    /// <see cref="AudioFormat.F32LE"/>.
    /// </summary>
    public static Sound LowPass(Sound sound, float cutoffHz)
    {
        ArgumentNullException.ThrowIfNull(sound);
        if (sound.Spec.Format != AudioFormat.F32LE)
            throw new NotSupportedException("Sound.LowPass currently supports F32LE only.");
        if (cutoffHz <= 0f)
            throw new ArgumentOutOfRangeException(nameof(cutoffHz));

        int sampleRate = sound.Spec.Frequency;
        double dt = 1.0 / sampleRate;
        double rc = 1.0 / (2.0 * Math.PI * cutoffHz);
        double a = dt / (rc + dt);

        var src = sound.Data.Span;
        int samples = src.Length / sizeof(float);
        var buffer = new byte[samples * sizeof(float)];
        var dst = buffer.AsSpan();
        double y = 0;
        for (int i = 0; i < samples; i++)
        {
            float x = BinaryPrimitives.ReadSingleLittleEndian(src.Slice(i * sizeof(float), sizeof(float)));
            y += a * (x - y);
            BinaryPrimitives.WriteSingleLittleEndian(dst.Slice(i * sizeof(float), sizeof(float)), (float)y);
        }
        return new Sound(sound.Spec, buffer);
    }

    /// <summary>
    /// One-pole low-pass filter whose cutoff sweeps exponentially from
    /// <paramref name="startCutoffHz"/> to <paramref name="endCutoffHz"/> over
    /// the duration of <paramref name="sound"/>. Pair with noise to fake
    /// engine throttle / wind / waterfall throttle effects.
    /// </summary>
    public static Sound LowPassSweep(Sound sound, float startCutoffHz, float endCutoffHz)
    {
        ArgumentNullException.ThrowIfNull(sound);
        if (sound.Spec.Format != AudioFormat.F32LE)
            throw new NotSupportedException("Sound.LowPassSweep currently supports F32LE only.");
        if (startCutoffHz <= 0f) throw new ArgumentOutOfRangeException(nameof(startCutoffHz));
        if (endCutoffHz <= 0f) throw new ArgumentOutOfRangeException(nameof(endCutoffHz));

        int sampleRate = sound.Spec.Frequency;
        double dt = 1.0 / sampleRate;
        var src = sound.Data.Span;
        int samples = src.Length / sizeof(float);
        var buffer = new byte[samples * sizeof(float)];
        var dst = buffer.AsSpan();
        double logStart = Math.Log(startCutoffHz);
        double logEnd = Math.Log(endCutoffHz);
        double y = 0;
        for (int i = 0; i < samples; i++)
        {
            double u = samples <= 1 ? 0 : (double)i / (samples - 1);
            double cutoff = Math.Exp(logStart + (logEnd - logStart) * u);
            double rc = 1.0 / (2.0 * Math.PI * cutoff);
            double a = dt / (rc + dt);
            float x = BinaryPrimitives.ReadSingleLittleEndian(src.Slice(i * sizeof(float), sizeof(float)));
            y += a * (x - y);
            BinaryPrimitives.WriteSingleLittleEndian(dst.Slice(i * sizeof(float), sizeof(float)), (float)y);
        }
        return new Sound(sound.Spec, buffer);
    }
    /// <summary>
    /// Synthesizes a single-pitch tone of the given waveform and duration.
    /// Mono, 32-bit float, 44.1 kHz. Pass the result to
    /// <see cref="Audio.Play(Sound, float)"/>.
    /// </summary>
    /// <param name="frequency">Pitch in Hertz (e.g. 440 = concert A).</param>
    /// <param name="duration">Length in seconds.</param>
    /// <param name="wave">Oscillator shape.</param>
    /// <param name="volume">Peak amplitude (0..1).</param>
    /// <param name="attack">Linear fade-in in seconds (prevents click).</param>
    /// <param name="release">Linear fade-out in seconds.</param>
    /// <param name="duty">Pulse width for <see cref="Waveform.Square"/> (0..1). Ignored for other waves.</param>
    /// <param name="vibratoHz">Frequency-modulation rate in Hz. 0 disables vibrato.</param>
    /// <param name="vibratoDepth">Vibrato amount as a fraction of the carrier frequency (e.g. 0.05 = ±5%).</param>
    public static Sound Tone(
        float frequency,
        float duration,
        Waveform wave = Waveform.Square,
        float volume = 0.5f,
        float attack = 0.005f,
        float release = 0.05f,
        float duty = 0.5f,
        float vibratoHz = 0f,
        float vibratoDepth = 0f)
    {
        return Synthesize(
            startHz: frequency,
            endHz: frequency,
            duration: duration,
            wave: wave,
            volume: volume,
            attack: attack,
            release: release,
            duty: duty,
            vibratoHz: vibratoHz,
            vibratoDepth: vibratoDepth);
    }

    /// <summary>
    /// Synthesizes a tone whose pitch glides from <paramref name="startHz"/>
    /// to <paramref name="endHz"/> over the duration (exponential lerp, so
    /// equal musical intervals take equal time). Great for zaps, jumps and
    /// laser sounds. Mono, 32-bit float, 44.1 kHz.
    /// </summary>
    public static Sound Sweep(
        float startHz,
        float endHz,
        float duration,
        Waveform wave = Waveform.Square,
        float volume = 0.5f,
        float attack = 0.005f,
        float release = 0.05f,
        float duty = 0.5f,
        float vibratoHz = 0f,
        float vibratoDepth = 0f)
    {
        return Synthesize(
            startHz: startHz,
            endHz: endHz,
            duration: duration,
            wave: wave,
            volume: volume,
            attack: attack,
            release: release,
            duty: duty,
            vibratoHz: vibratoHz,
            vibratoDepth: vibratoDepth);
    }

    private static Sound Synthesize(
        float startHz,
        float endHz,
        float duration,
        Waveform wave,
        float volume,
        float attack,
        float release,
        float duty,
        float vibratoHz,
        float vibratoDepth)
    {
        if (duration <= 0f)
            return new Sound(new AudioSpec(AudioFormat.F32LE, 1, SynthSampleRate), ReadOnlyMemory<byte>.Empty);

        int sampleRate = SynthSampleRate;
        int sampleCount = Math.Max(1, (int)MathF.Round(duration * sampleRate));
        var bytes = new byte[sampleCount * sizeof(float)];
        var span = bytes.AsSpan();

        // Phase accumulator in cycles [0,1). Works for sweep + vibrato because
        // we advance by (instantaneousHz / sampleRate) each step instead of
        // sampling sin(2πft) directly (which would phase-jump on freq change).
        double phase = 0;
        double logStart = Math.Log(Math.Max(1e-3, startHz));
        double logEnd = Math.Log(Math.Max(1e-3, endHz));
        bool sweeping = !startHz.Equals(endHz);

        int attackSamples = Math.Max(1, (int)MathF.Round(attack * sampleRate));
        int releaseSamples = Math.Max(1, (int)MathF.Round(release * sampleRate));
        // Clamp so attack+release fits inside the buffer.
        if (attackSamples + releaseSamples > sampleCount)
        {
            attackSamples = sampleCount / 2;
            releaseSamples = sampleCount - attackSamples;
        }

        // Deterministic per-call PRNG for noise so the same call returns
        // the same buffer (handy for tests and reproducible SFX).
        uint rng = unchecked((uint)HashCode.Combine(
            startHz, endHz, duration, wave, duty, vibratoHz, vibratoDepth));
        if (rng == 0) rng = 0x9E3779B9u;

        // Brown-noise state: integrated white noise with a small decay so the
        // random walk doesn't drift to ±∞. Only used by Waveform.BrownNoise.
        double brown = 0;

        double invSampleRate = 1.0 / sampleRate;

        for (int i = 0; i < sampleCount; i++)
        {
            double t = i * invSampleRate;
            double u = sampleCount > 1 ? (double)i / (sampleCount - 1) : 0.0;

            double hz = sweeping ? Math.Exp(logStart + (logEnd - logStart) * u) : startHz;
            if (vibratoHz > 0f && vibratoDepth > 0f)
                hz *= 1.0 + vibratoDepth * Math.Sin(2.0 * Math.PI * vibratoHz * t);

            phase += hz * invSampleRate;
            if (phase >= 1.0) phase -= Math.Floor(phase);

            double sample = wave switch
            {
                Waveform.Sine => Math.Sin(phase * 2.0 * Math.PI),
                Waveform.Square => phase < duty ? 1.0 : -1.0,
                Waveform.Triangle => 4.0 * Math.Abs(phase - 0.5) - 1.0,
                Waveform.Sawtooth => 2.0 * phase - 1.0,
                Waveform.Noise => NextNoise(ref rng),
                Waveform.BrownNoise => NextBrown(ref rng, ref brown),
                _ => 0.0,
            };

            // Linear attack/release envelope.
            float env = 1f;
            if (i < attackSamples)
                env = (float)i / attackSamples;
            else if (i >= sampleCount - releaseSamples)
                env = (float)(sampleCount - 1 - i) / releaseSamples;

            float value = (float)(sample * volume * env);
            BinaryPrimitives.WriteSingleLittleEndian(span.Slice(i * sizeof(float), sizeof(float)), value);
        }

        return new Sound(new AudioSpec(AudioFormat.F32LE, 1, sampleRate), bytes);
    }

    // xorshift32 → -1..1 float.
    private static double NextNoise(ref uint state)
    {
        uint x = state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        state = x;
        return (x / (double)uint.MaxValue) * 2.0 - 1.0;
    }

    // Brown noise: leaky integrator over white. The 0.02/1.02 coefficients
    // and 3.5× scale are the standard Voss formula and yield a roughly ±1
    // peak signal that sounds like deep rumble rather than hiss.
    private static double NextBrown(ref uint state, ref double brown)
    {
        double white = NextNoise(ref state);
        brown = (brown + 0.02 * white) / 1.02;
        return brown * 3.5;
    }
}

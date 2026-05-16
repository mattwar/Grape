using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Blitter.Bits;

/// <summary>
/// Adds <c>Sound.CreateMelody</c> — parses a compact text score into a <see cref="Sound"/>.
/// Also adds <c>Audio.PlayMelody</c> for fire-and-forget playback with auto-caching.
/// </summary>
public static class SoundMelodyExtensions
{
    private static readonly int[] LetterSemitones = { 9, 11, 0, 2, 4, 5, 7 }; // a, b, c, d, e, f, g

    // Two-level cache for Audio.PlayMelody:
    //   outer: ConditionalWeakTable<score-string, params->Sound dictionary>
    // The outer key is the score string itself. When it is GC'd, the entire
    // params dictionary goes with it — so string literals (app-lifetime,
    // interned) stay cached forever, while dynamically-built scores release
    // their cached Sound once no one references the string. Sounds are
    // rendered at amplitude 1.0 so volume isn't part of the cache identity.
    private static readonly ConditionalWeakTable<string, ConcurrentDictionary<MelodyParams, Sound>> Cache = new();

    private readonly record struct MelodyParams(int Bpm, Waveform Wave, int DefaultOctave, float Gap);

    extension(Sound)
    {
        /// <summary>
        /// Creates a <see cref="Sound"/> from a series of notes and rests, expressed as MIDI tokens.
        /// </summary>
        /// <param name="score">Whitespace-separated note/rest tokens. examples: A, C#, B4, F:2, -, R:2</param>
        /// <param name="bpm">Tempo in beats per minute. One bare note = one beat.</param>
        /// <param name="wave">Oscillator for each note.</param>
        /// <param name="volume">Peak amplitude (0..1).</param>
        /// <param name="defaultOctave">Octave used when a token omits the octave digit.</param>
        /// <param name="gap">Fraction of each note's duration left silent at the end so adjacent notes are distinguishable (0..1).</param>
        public static Sound CreateMelody(
            string score,
            int bpm = 120,
            Waveform wave = Waveform.Square,
            float volume = 0.5f,
            int defaultOctave = 4,
            float gap = 0.1f)
                => BuildMelody(score, bpm, wave, volume, defaultOctave, gap);
    }

    extension(Audio)
    {
        /// <summary>
        /// Plays a melody score in fire-and-forget fashion. The rendered
        /// <see cref="Sound"/> is cached by (score, bpm, wave, defaultOctave, gap),
        /// so repeated calls with the same arguments don't re-synthesize.
        /// </summary>
        /// <param name="score">Whitespace-separated note/rest tokens. See <c>Sound.CreateMelody</c>.</param>
        /// <param name="bpm">Tempo in beats per minute.</param>
        /// <param name="wave">Oscillator for each note.</param>
        /// <param name="volume">Playback volume (0..1). Does not affect cache identity.</param>
        /// <param name="defaultOctave">Octave used when a token omits the octave digit.</param>
        /// <param name="gap">Fraction of each note's duration left silent at the end (0..1).</param>
        public static void PlayMelody(
            string score,
            int bpm = 120,
            Waveform wave = Waveform.Square,
            float volume = 1f,
            int defaultOctave = 4,
            float gap = 0.1f)
        {
            Audio.Play(GetOrBuildCached(score, bpm, wave, defaultOctave, gap), volume);
        }

        /// <summary>
        /// Plays a melody score asynchronously, completing when playback ends.
        /// Caches the rendered <see cref="Sound"/> the same way as
        /// <c>Audio.PlayMelody</c>.
        /// </summary>
        /// <param name="score">Whitespace-separated note/rest tokens.</param>
        /// <param name="bpm">Tempo in beats per minute.</param>
        /// <param name="wave">Oscillator for each note.</param>
        /// <param name="volume">Playback volume (0..1). Does not affect cache identity.</param>
        /// <param name="defaultOctave">Octave used when a token omits the octave digit.</param>
        /// <param name="gap">Fraction of each note's duration left silent at the end (0..1).</param>
        public static Task PlayMelodyAsync(
            string score,
            int bpm = 120,
            Waveform wave = Waveform.Square,
            float volume = 1f,
            int defaultOctave = 4,
            float gap = 0.1f)
        {
            return Audio.PlayAsync(GetOrBuildCached(score, bpm, wave, defaultOctave, gap), volume);
        }
    }

    private static Sound GetOrBuildCached(string score, int bpm, Waveform wave, int defaultOctave, float gap)
    {
        ArgumentNullException.ThrowIfNull(score);
        var subCache = Cache.GetValue(score, static _ => new ConcurrentDictionary<MelodyParams, Sound>());
        var key = new MelodyParams(bpm, wave, defaultOctave, gap);
        return subCache.GetOrAdd(key,
            (k, s) => BuildMelody(s, k.Bpm, k.Wave, 1f, k.DefaultOctave, k.Gap),
            score);
    }

    private static Sound BuildMelody(string score, int bpm, Waveform wave, float volume, int defaultOctave, float gap)
    {
        ArgumentNullException.ThrowIfNull(score);
        if (bpm <= 0) throw new ArgumentOutOfRangeException(nameof(bpm));
        if (gap < 0f || gap >= 1f) throw new ArgumentOutOfRangeException(nameof(gap));

        float secondsPerBeat = 60f / bpm;
        var tokens = score.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var parts = new List<Sound>(tokens.Length);

        for (int t = 0; t < tokens.Length; t++)
        {
            var token = tokens[t];
            if (token == "|") continue;

            // Split on ':' for duration.
            float beats = 1f;
            string head = token;
            int colon = token.IndexOf(':');
            if (colon >= 0)
            {
                head = token.Substring(0, colon);
                var durStr = token.Substring(colon + 1);
                if (!float.TryParse(durStr, NumberStyles.Float, CultureInfo.InvariantCulture, out beats) || beats <= 0f)
                    throw new FormatException($"Invalid duration in token '{token}'.");
            }

            float duration = beats * secondsPerBeat;
            float noteDur = duration * (1f - gap);
            float restDur = duration - noteDur;

            // Rest?
            if (head.Length == 0 || head == "r" || head == "R" || head == "-")
            {
                parts.Add(Silence(duration));
                continue;
            }

            int midi = ParseNote(head, defaultOctave, token);
            float freq = 440f * MathF.Pow(2f, (midi - 69) / 12f);
            parts.Add(Sound.Tone(freq, noteDur, wave, volume, attack: 0.005f, release: 0.015f));
            if (restDur > 0f)
                parts.Add(Silence(restDur));
        }

        return Sound.Concat(parts.ToArray());
    }

    private static int ParseNote(string head, int defaultOctave, string original)
    {
        // <letter>[accidental][octave]
        char letter = char.ToLowerInvariant(head[0]);
        if (letter < 'a' || letter > 'g')
            throw new FormatException($"Invalid note letter in token '{original}'.");
        int semis = LetterSemitones[letter - 'a'];

        int idx = 1;
        int accidental = 0;
        if (idx < head.Length)
        {
            char c = head[idx];
            if (c == '#' || c == 's' || c == 'S') 
            { 
                accidental = 1; 
                idx++; 
            }
            else if (c == 'b' || c == 'B') 
            { 
                accidental = -1; 
                idx++; 
            }
        }

        int octave = defaultOctave;
        if (idx < head.Length)
        {
            var octStr = head.Substring(idx);
            if (!int.TryParse(octStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out octave))
                throw new FormatException($"Invalid octave in token '{original}'.");
        }

        // MIDI: C-1 = 0, C0 = 12, C4 = 60, A4 = 69.
        return 12 * (octave + 1) + semis + accidental;
    }

    private static Sound Silence(float duration)
    {
        // Silent buffer at the standard synth spec.
        const int rate = 44100;
        int samples = Math.Max(0, (int)MathF.Round(duration * rate));
        var buffer = new byte[samples * sizeof(float)];
        return new Sound(new Devices.AudioSpec(Devices.AudioFormat.F32LE, 1, rate), buffer);
    }
}

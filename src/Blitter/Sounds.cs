namespace Blitter;

/// <summary>
/// Ready-made retro / chiptune sound effects.
/// </summary>
/// <remarks>
/// Each preset is exposed two ways:
/// <list type="bullet">
///   <item>A static property (e.g. <see cref="Coin"/>) holding a pre-built
///     <see cref="Sound"/> with sensible defaults — synthesized once and
///     reused. Pass it directly to <see cref="Audio.Play(Sound, float)"/>.</item>
///   <item>A <c>CreateXxx(...)</c> factory (e.g. <see cref="CreateCoin"/>)
///     when you want to tweak volume, duration or other parameters.</item>
/// </list>
/// </remarks>
public static class Sounds
{
    /// <summary>Short high square tick — UI navigate, cursor blip.</summary>
    public static Sound Blip { get; } = CreateBlip();

    /// <summary>Even shorter, sharper square tick — menu select / confirm.</summary>
    public static Sound Select { get; } = CreateSelect();

    /// <summary>Two-tone upward arpeggio — Mario-style coin pickup.</summary>
    public static Sound Coin { get; } = CreateCoin();

    /// <summary>Upward pitch sweep — character jump.</summary>
    public static Sound Jump { get; } = CreateJump();

    /// <summary>Fast downward sweep — laser / pew.</summary>
    public static Sound Laser { get; } = CreateLaser();

    /// <summary>Noise burst with long tail — explosion / crash.</summary>
    public static Sound Explosion { get; } = CreateExplosion();

    /// <summary>Noise tick plus a low square thud — player takes a hit.</summary>
    public static Sound Hurt { get; } = CreateHurt();

    /// <summary>Ascending 4-note square arpeggio — power-up / level cleared.</summary>
    public static Sound PowerUp { get; } = CreatePowerUp();

    /// <summary>Slow up-and-down wail — police / alert siren.</summary>
    public static Sound Siren { get; } = CreateSiren();

    /// <summary>Two-tone honking alarm — air-raid / klaxon.</summary>
    public static Sound Klaxon { get; } = CreateKlaxon();

    /// <summary>Fast vibrato square — wobbly UFO / warble.</summary>
    public static Sound Warble { get; } = CreateWarble();

    /// <summary>Short downward square chirp — Pong-style wall bounce.</summary>
    public static Sound Bounce { get; } = CreateBounce();

    /// <summary>Cartoon "boing" — downward triangle sweep with heavy vibrato.</summary>
    public static Sound Boing { get; } = CreateBoing();

    /// <summary>Climbing arcade warble — pitched square sweep rising with vibrato + echo.</summary>
    public static Sound RoarUp { get; } = CreateRoarUp();

    /// <summary>Falling arcade warble — pitched square sweep dropping with vibrato + echo.</summary>
    public static Sound RoarDown { get; } = CreateRoarDown();

    // -------------------- Factories --------------------

    // Interval ratios (equal temperament).
    private const float PerfectFourth = 1.33484f;  // 2^(5/12)
    private const float MajorThird    = 1.25992f;  // 2^(4/12)
    private const float PerfectFifth  = 1.49831f;  // 2^(7/12)

    public static Sound CreateBlip(float frequency = Notes.B5, float volume = 0.5f) =>
        Sound.Tone(frequency, 0.06f, Waveform.Square, volume);

    public static Sound CreateSelect(float frequency = Notes.Fs6, float volume = 0.5f) =>
        Sound.Tone(frequency, 0.04f, Waveform.Square, volume, duty: 0.25f);

    /// <summary>Two-tone upward arpeggio. Second note is a perfect fourth above <paramref name="rootNote"/>.</summary>
    public static Sound CreateCoin(float rootNote = Notes.B5, float volume = 0.5f) =>
        Concat(
            Sound.Tone(rootNote, 0.08f, Waveform.Square, volume),
            Sound.Tone(rootNote * PerfectFourth, 0.32f, Waveform.Square, volume));

    /// <summary>Upward pitch sweep covering two octaves above <paramref name="startNote"/>.</summary>
    public static Sound CreateJump(float startNote = Notes.Gs3, float volume = 0.5f) =>
        Sound.Sweep(startNote, startNote * 4f, 0.15f, Waveform.Square, volume);

    /// <summary>Fast downward sweep covering three octaves below <paramref name="startNote"/>.</summary>
    public static Sound CreateLaser(float startNote = Notes.A5, float volume = 0.5f) =>
        Sound.Sweep(startNote, startNote / 8f, 0.25f, Waveform.Square, volume);

    public static Sound CreateExplosion(float volume = 0.6f) =>
        Sound.Tone(0f, 0.6f, Waveform.Noise, volume, attack: 0.001f, release: 0.45f);

    /// <summary>Noise tick plus a low square thud at <paramref name="lowNote"/>.</summary>
    public static Sound CreateHurt(float lowNote = Notes.Cs3, float volume = 0.5f) =>
        Concat(
            Sound.Tone(0f, 0.08f, Waveform.Noise, volume, release: 0.06f),
            Sound.Tone(lowNote, 0.18f, Waveform.Square, volume * 0.8f));

    /// <summary>Ascending major arpeggio (root, M3, P5, octave) — power-up / level cleared.</summary>
    public static Sound CreatePowerUp(float rootNote = Notes.C5, float volume = 0.5f) =>
        Concat(
            Sound.Tone(rootNote,                0.08f, Waveform.Square, volume),
            Sound.Tone(rootNote * MajorThird,   0.08f, Waveform.Square, volume),
            Sound.Tone(rootNote * PerfectFifth, 0.08f, Waveform.Square, volume),
            Sound.Tone(rootNote * 2f,           0.20f, Waveform.Square, volume));

    /// <summary>Slow up-and-down wail centered on <paramref name="centerNote"/>.</summary>
    public static Sound CreateSiren(float duration = 1.5f, float centerNote = Notes.D5, float volume = 0.4f) =>
        Sound.Tone(
            frequency: centerNote,
            duration: duration,
            wave: Waveform.Square,
            volume: volume,
            vibratoHz: 1.5f,
            vibratoDepth: 0.45f);

    /// <summary>Alternating two-tone alarm. Lower note is a perfect fourth below <paramref name="rootNote"/>.</summary>
    public static Sound CreateKlaxon(float duration = 1.0f, float rootNote = Notes.A4, float volume = 0.5f)
    {
        const float beat = 0.15f;
        int beats = Math.Max(2, (int)MathF.Round(duration / beat));
        float lowNote = rootNote / PerfectFourth;
        var parts = new Sound[beats];
        for (int i = 0; i < beats; i++)
        {
            float hz = (i & 1) == 0 ? rootNote : lowNote;
            parts[i] = Sound.Tone(hz, beat, Waveform.Sawtooth, volume, duty: 0.5f, release: 0.02f);
        }
        return Concat(parts);
    }

    /// <summary>Fast vibrato centered on <paramref name="centerNote"/> — wobbly UFO.</summary>
    public static Sound CreateWarble(float duration = 0.8f, float centerNote = Notes.G5, float volume = 0.4f) =>
        Sound.Tone(
            frequency: centerNote,
            duration: duration,
            wave: Waveform.Square,
            volume: volume,
            vibratoHz: 14f,
            vibratoDepth: 0.18f);

    /// <summary>Short downward chirp sweeping an octave below <paramref name="startNote"/>.</summary>
    public static Sound CreateBounce(float startNote = Notes.F5, float volume = 0.5f) =>
        Sound.Sweep(startNote, startNote / 2f, 0.07f, Waveform.Square, volume,
            attack: 0.001f, release: 0.03f);

    /// <summary>Cartoon "boing" — downward triangle sweep covering ~21 semitones with heavy vibrato.</summary>
    public static Sound CreateBoing(float startNote = Notes.D5, float volume = 0.5f) =>
        Sound.Sweep(startNote, startNote * 0.3f, 0.35f, Waveform.Triangle, volume,
            attack: 0.002f, release: 0.18f,
            vibratoHz: 18f, vibratoDepth: 0.35f);

    /// <summary>Climbing rocket throttle — brown noise with rising low-pass cutoff (engine spool-up).</summary>
    public static Sound CreateRoarUp(float duration = 0.9f, float volume = 0.9f) =>
        Sound.LowPassSweep(
            Sound.Tone(0f, duration, Waveform.BrownNoise, volume,
                attack: 0.05f, release: 0.1f),
            startCutoffHz: 200f, endCutoffHz: 2000f);

    /// <summary>Falling rocket throttle — brown noise with dropping low-pass cutoff (engine spool-down).</summary>
    public static Sound CreateRoarDown(float duration = 0.9f, float volume = 0.9f) =>
        Sound.LowPassSweep(
            Sound.Tone(0f, duration, Waveform.BrownNoise, volume,
                attack: 0.05f, release: 0.1f),
            startCutoffHz: 2000f, endCutoffHz: 200f);

    private static Sound Concat(params Sound[] parts) => Sound.Concat(parts);
}

namespace Blitter.Bits;

/// <summary>
/// Short, original, non-copyright melodic stings keyed to common game moods.
/// </summary>
/// <remarks>
/// Each preset is exposed two ways:
/// <list type="bullet">
///   <item>A static property (e.g. <see cref="Victory"/>) — synthesized once and reused.</item>
///   <item>A <c>CreateXxx(...)</c> factory accepting <c>bpm</c> and <c>volume</c> overrides.</item>
/// </list>
/// </remarks>
public static class Melodies
{
    /// <summary>Low minor line with a tritone — creepy, restless.</summary>
    public static Sound Spooky { get; } = CreateSpooky();

    /// <summary>Very low, slow minor sigh — eerie, mournful.</summary>
    public static Sound Haunting { get; } = CreateHaunting();

    /// <summary>Whole-tone zig-zag ending on a held high note — questioning, suspenseful.</summary>
    public static Sound Mystery { get; } = CreateMystery();

    /// <summary>C major arpeggio rising and returning — bright and open.</summary>
    public static Sound Sunny { get; } = CreateSunny();

    /// <summary>Bouncy major triad jig — cheerful and light.</summary>
    public static Sound Happy { get; } = CreateHappy();

    /// <summary>Ascending V-I fanfare — triumphant.</summary>
    public static Sound Victory { get; } = CreateVictory();

    /// <summary>Classic descending "wah-wah" cadence — sad / game over.</summary>
    public static Sound Defeat { get; } = CreateDefeat();

    /// <summary>Quick ascending major arpeggio with octave leap — power-up / level cleared.</summary>
    public static Sound LevelUp { get; } = CreateLevelUp();

    // -------------------- Factories --------------------

    public static Sound CreateSpooky(int bpm = 90, float volume = 0.4f) =>
        Sound.CreateMelody(
            "a3 c4 ds4 e4 ds4 c4 a3:2",
            bpm: bpm, wave: Waveform.Triangle, volume: volume);

    public static Sound CreateHaunting(int bpm = 110, float volume = 0.5f) =>
        Sound.CreateMelody(
            "a2:3 c3:3 d3:3 ds3:3 d3:3 c3:3 a2:6",
            bpm: bpm, wave: Waveform.Triangle, volume: volume);

    public static Sound CreateMystery(int bpm = 110, float volume = 0.4f) =>
        Sound.CreateMelody(
            "c4 fs4 d4 gs4 e4 as4 fs4 c5:3",
            bpm: bpm, wave: Waveform.Triangle, volume: volume);

    public static Sound CreateSunny(int bpm = 140, float volume = 0.45f) =>
        Sound.CreateMelody(
            "c5 e5 g5 c6 g5 e5 c5:2",
            bpm: bpm, wave: Waveform.Square, volume: volume);

    public static Sound CreateHappy(int bpm = 180, float volume = 0.45f) =>
        Sound.CreateMelody(
            "c5:.5 e5:.5 g5:.5 c6 g5:.5 e5:.5 c5:.5 e5 c5:2",
            bpm: bpm, wave: Waveform.Square, volume: volume);

    public static Sound CreateVictory(int bpm = 150, float volume = 0.5f) =>
        Sound.CreateMelody(
            "g4:.5 c5:.5 e5:.5 g5:.5 c6:.5 e6:.5 g6:2",
            bpm: bpm, wave: Waveform.Square, volume: volume);

    public static Sound CreateDefeat(int bpm = 110, float volume = 0.45f) =>
        Sound.CreateMelody(
            "c5 b4 as4 a4:3",
            bpm: bpm, wave: Waveform.Triangle, volume: volume);

    public static Sound CreateLevelUp(int bpm = 220, float volume = 0.5f) =>
        Sound.CreateMelody(
            "c5:.5 e5:.5 g5:.5 c6",
            bpm: bpm, wave: Waveform.Square, volume: volume);
}

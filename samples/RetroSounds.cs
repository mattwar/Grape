#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/RetroSounds.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

using Blitter;
using Blitter.Bits;

// Demonstrates the procedural retro-sound helpers (Sound.Tone, Sound.Sweep)
// and the named presets in `Sounds`. Press the matching number key to
// audition each one.

const int W = 960;
const int H = 540;

var window = new Window2D(W, H)
{
    Title = "Retro Sounds (press 1-9, 0, Q, B, G, R, F, E, M)",
    BackgroundColor = new Color(8, 8, 24),
    FullScreen = false,
    CloseKey = Key.Escape,
};

window.Renderer.SetLogicalSize(W, H, LogicalPresentation.Letterbox);

(Key Key, string Label, Sound Sound)[] entries =
[
    (Key.D1, "1  Blip",      Sounds.Blip),
    (Key.D2, "2  Select",    Sounds.Select),
    (Key.D3, "3  Coin",      Sounds.Coin),
    (Key.D4, "4  Jump",      Sounds.Jump),
    (Key.D5, "5  Laser",     Sounds.Laser),
    (Key.D6, "6  Explosion", Sounds.Explosion),
    (Key.D7, "7  Hurt",      Sounds.Hurt),
    (Key.D8, "8  PowerUp",   Sounds.PowerUp),
    (Key.D9, "9  Siren",     Sounds.Siren),
    (Key.D0, "0  Klaxon",    Sounds.Klaxon),
    (Key.Q,  "Q  Warble",    Sounds.Warble),
    (Key.B,  "B  Bounce",    Sounds.Bounce),
    (Key.G,  "G  Boing",     Sounds.Boing),
    (Key.R,  "R  RoarUp",    Sounds.RoarUp),
    (Key.F,  "F  RoarDown",  Sounds.RoarDown),
    (Key.E,  "E  Coin+Echo", Sound.Echo(Sounds.Coin, delay: 0.18f, decay: 0.5f, repeats: 4)),
    (Key.M,  "M  Melody (Twinkle)",
        Sound.CreateMelody("c c g g a a g:2 f f e e d d c:2", bpm: 240, wave: Waveform.Triangle)),
    (Key.S,  "S  Mood: Spooky",   Melodies.Spooky),
    (Key.H,  "H  Mood: Haunting", Melodies.Haunting),
    (Key.Y,  "Y  Mood: Mystery",  Melodies.Mystery),
    (Key.U,  "U  Mood: Sunny",    Melodies.Sunny),
    (Key.J,  "J  Mood: Happy",    Melodies.Happy),
    (Key.V,  "V  Mood: Victory",  Melodies.Victory),
    (Key.X,  "X  Mood: Defeat",   Melodies.Defeat),
    (Key.L,  "L  Mood: LevelUp",  Melodies.LevelUp),
];

string lastPlayed = "(press a key)";
double flashUntil = 0;

await window.RunAsync(rd =>
{
    var input = window.Input;
    var now = rd.ElapsedSecondsSinceStart;

    foreach (var entry in entries)
    {
        if (input.WasJustPressed(entry.Key))
        {
            Audio.Play(entry.Sound);
            lastPlayed = entry.Label;
            flashUntil = now + 0.25;
        }
    }

    // Header.
    rd.DrawColor = new Color(180, 220, 255);
    rd.DrawDebugText(20, 16, "Retro sound presets", scale: 3f);
    rd.DrawColor = new Color(120, 140, 180);
    rd.DrawDebugText(20, 56, "Press a key to play. ESC to quit.", scale: 2f);

    // Preset list — two columns to fit everything on screen.
    int startY = 100;
    int lineH = 28;
    int colWidth = 280;
    int rowsPerCol = (H - startY - 60) / lineH;
    for (int i = 0; i < entries.Length; i++)
    {
        var entry = entries[i];
        int col = i / rowsPerCol;
        int row = i % rowsPerCol;
        int x = 40 + col * colWidth;
        int y = startY + row * lineH;
        rd.DrawColor = entry.Label == lastPlayed && now < flashUntil
            ? new Color(255, 255, 120)
            : new Color(230, 230, 230);
        rd.DrawDebugText(x, y, entry.Label, scale: 2f);
    }

    // Last played readout.
    rd.DrawColor = new Color(120, 200, 120);
    rd.DrawDebugText(20, H - 36, $"last: {lastPlayed}", scale: 2f);
});

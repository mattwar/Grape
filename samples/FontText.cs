#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/FontText.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Demonstrates Blitter.Bits.Font: a Skia-baked monospace bitmap font
// rendered into a 3D scene as world-space labels. Each Font instance
// owns one atlas image with the chosen color baked in, so several
// colors mean several Font instances. Glyph quads are built per
// DrawText call as a fresh textured-quad mesh.

using System.Numerics;
using Blitter;
using Blitter.Bits;

// CSS-style fallback list: takes the first family installed on the host
// OS. Consolas is on Windows, Menlo on macOS, DejaVu Sans Mono on most
// Linux distros. If none of these are present, Font falls back to Skia's
// generic "monospace" alias automatically.
string[] mono = ["Consolas", "Menlo", "DejaVu Sans Mono"];

// Two fonts: a large white headline and a smaller cyan readout.
// ghostFont extends the default printable-ASCII charset with the card-suit
// codepoints (Unicode U+2660..U+2667), so the orbiting ghost lines can mix
// letters and symbols. Codepoints not in a font's charset render blank.
using var titleFont   = new Font(mono, 64, new Color(240, 240, 255), bold: true);
using var readoutFont = new Font(mono, 32, new Color(120, 220, 255));
using var ghostFont   = new Font(mono, 32, new Color(140, 255, 110, 180),
                                 charset: FontCharsets.AsciiPrintable + "♥♦♣♠");

var window = new Window3D
{
    Title = "Font (Blitter.Bits)",
    BackgroundColor = new Color(10, 12, 22),
    FullScreen = true,
    CloseKey = Key.Escape,
    AutoInvalidate = true,
};

long frameCount = 0;

window.Rendering += (w, rd) =>
{
    frameCount++;
    var t = rd.ElapsedSecondsSinceStart;
    var aspect = 1f / rd.AspectRatio; // height / width

    // Title banner: fixed string, gentle bob.
    {
        const string title = "Blitter.Bits.Font";
        float scale = 0.08f;
        float bob   = 0.03f * MathF.Sin(t * 1.6f);
        float widthInNdc = title.Length * scale;
        // Font glyphs are 1 unit wide in local space; translate by half-length
        // to center, then scale, then position in NDC.
        var transform =
            Matrix4x4.CreateTranslation(-title.Length / 2f, 0f, 0f) *
            Matrix4x4.CreateScale(scale) *
            Matrix4x4.CreateTranslation(0f, 0.45f + bob, 0f) *
            Matrix4x4.CreateScale(aspect, 1f, 1f);
        titleFont.DrawText(rd, title, transform);
    }

    // Live readout under the title.
    {
        var live = $"t={t:F2}s   frame={frameCount}";
        float scale = 0.05f;
        var transform =
            Matrix4x4.CreateTranslation(-live.Length / 2f, 0f, 0f) *
            Matrix4x4.CreateScale(scale) *
            Matrix4x4.CreateTranslation(0f, 0.25f, 0f) *
            Matrix4x4.CreateScale(aspect, 1f, 1f);
        readoutFont.DrawText(rd, live, transform);
    }

    // Three ghost lines orbiting in the lower half. Each spins around
    // its own Y axis so you can see the text is real 3D geometry, not
    // a flat overlay -- the glyphs go edge-on and disappear, then
    // re-emerge mirrored.
    for (int i = 0; i < 3; i++)
    {
        string s = i switch
        {
            0 => "♥ MONOSPACE GLYPHS ♥",
            1 => "♦ BAKED COLOR PER FONT ♦",
            _ => "♠ MESH CACHED PER STRING ♠",
        };
        float phase = t * 0.6f + i * MathF.Tau / 3f;
        float x = 0.55f * MathF.Cos(phase);
        float y = -0.25f + 0.1f * MathF.Sin(phase * 1.7f);
        float scale = 0.04f;
        float spin = t * 1.4f + i * MathF.Tau / 3f;
        var transform =
            // Center the text on its midpoint so the spin pivots through
            // the middle instead of swinging around an end.
            Matrix4x4.CreateTranslation(-s.Length / 2f, -0.5f, 0f) *
            Matrix4x4.CreateRotationY(spin) *
            Matrix4x4.CreateRotationX(0.25f * MathF.Sin(phase * 1.3f)) *
            Matrix4x4.CreateScale(scale) *
            Matrix4x4.CreateTranslation(x, y, 0f) *
            Matrix4x4.CreateScale(aspect, 1f, 1f);
        ghostFont.DrawText(rd, s, transform);
    }
};

await window.WaitForCloseAsync();

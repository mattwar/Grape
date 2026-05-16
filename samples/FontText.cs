#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/FontText.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Demonstrates Blitter.Bits.Font: render text in any font.

using System.Numerics;
using Blitter;
using Blitter.Bits;

// similar fonts for different platforms
string[] mono = ["Consolas", "Menlo", "DejaVu Sans Mono"];

using var titleFont   = new Font(mono, 64, bold: true);
using var readoutFont = new Font(mono, 32);
using var ghostFont   = new Font(mono, 32, charset: FontCharsets.AsciiPrintable + "♥♦♣♠");

var titleColor   = new Color(240, 240, 255);
var readoutColor = new Color(120, 220, 255);
var ghostColors  = new []{Color.Red, Color.Yellow, Color.Blue};

var window = new Window3D
{
    Title = "Font (Blitter.Bits)",
    BackgroundColor = new Color(10, 12, 22),
    FullScreen = true,
    CloseKey = Key.Escape,
};

long frameCount = 0;

await window.RunAsync(rd =>
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
        var transform = Matrix4x4.CreateTranslation(-title.Length / 2f, 0f, 0f)
            .Scale(scale)
            .Translate(0f, 0.45f + bob, 0f)
            .Scale(aspect, 1f, 1f);
        titleFont.DrawText(rd, title, titleColor, transform);
    }

    // Live readout under the title.
    {
        var live = $"t={t:F2}s   frame={frameCount}";
        float scale = 0.05f;
        var transform = Matrix4x4.CreateTranslation(-live.Length / 2f, 0f, 0f)
            .Scale(scale)
            .Translate(0f, 0.25f, 0f)
            .Scale(aspect, 1f, 1f);
        readoutFont.DrawText(rd, live, readoutColor, transform);
    }

    // Three ghost lines orbiting in the lower half. 
    // Each spins around its own Y axis so you can see the text is real 3D geometry.
    for (int i = 0; i < 3; i++)
    {
        string s = i switch
        {
            0 => "♥ MONOSPACE GLYPHS ♥",
            1 => "♦ DRAW IN ANY COLOR ♦",
            _ => "♠ CACHED MESHES PER STRING ♠",
        };
        float phase = t * 0.6f + i * MathF.Tau / 3f;
        float x = 0.55f * MathF.Cos(phase);
        float y = -0.25f + 0.1f * MathF.Sin(phase * 1.7f);
        float scale = 0.08f;
        float spin = t * 1.4f + i * MathF.Tau / 3f;
        var transform = Matrix4x4.CreateTranslation(-s.Length / 2f, -0.5f, 0f)
            .RotateY(spin)
            .RotateX(0.25f * MathF.Sin(phase * 1.3f))
            .Scale(scale)
            .Translate(x, y, 0f)
            .Scale(aspect, 1f, 1f);
        ghostFont.DrawText(rd, s, ghostColors[i], transform);
    }
});
#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/SkiaBitmap.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Demonstrates how tightly SkiaSharp integrates with Blitter:
// `Image.DrawCanvas(canvas => ...)` hands you an SKCanvas pointing
// at the image's pixels, so you can use any Skia drawing code to
// build textures the renderer can blit. Here we bake an atlas once
// at startup, then blit cells of it hundreds of times per frame as
// falling confetti.
//
// If you already have an SKBitmap from another source (PNG decode,
// procedural pipeline, etc.), `bitmap.ToImage()` adopts it instead.
//
// For per-frame procedural drawing (where the pixels actually change
// every frame) use `Renderer2D.DrawCanvas(rect, action)` instead;
// see the SkiaCanvas sample.

using System.Numerics;
using Blitter;
using Blitter.Bits;
using SkiaSharp;

const int CellSize  = 96;
const int AtlasCols = 4;
const int AtlasRows = 4;
const int AtlasW    = CellSize * AtlasCols;
const int AtlasH    = CellSize * AtlasRows;
const int ConfettiCount = 320;

// All draws inside the rendering callback work in this fixed design
// space. The renderer's ViewPort + Scale letterbox it into whatever
// the actual window size is, so the layout stays aspect-correct on
// resize / fullscreen.
const int DesignW = 960;
const int DesignH = 540;

char[] glyphs =
[
    'B','L','I','T',
    'T','E','R','+',
    'S','K','I','A',
    '!','*','#','@',
];

SKColor[] hues =
[
    new(0xFF, 0x6B, 0x9E), new(0x4E, 0x9C, 0xFF), new(0x6B, 0xFF, 0xC2), new(0xFF, 0xC8, 0x4E),
    new(0xC8, 0x6B, 0xFF), new(0x6B, 0xE0, 0xFF), new(0xFF, 0x9E, 0x4E), new(0x9E, 0xFF, 0x6B),
    new(0xFF, 0x4E, 0x6B), new(0x4E, 0xFF, 0xC8), new(0xC2, 0xFF, 0x6B), new(0x6B, 0x6B, 0xFF),
    new(0xFF, 0xFF, 0x6B), new(0x6B, 0xFF, 0x9E), new(0xFF, 0x6B, 0x4E), new(0x4E, 0x6B, 0xFF),
];

// Build the atlas once. Per cell: a diagonal gradient backdrop and
// an anti-aliased character centered on top. `Image.DrawCanvas`
// gives us an SKCanvas pointing at the image's pixels -- everything
// inside the lambda is plain SkiaSharp code.
var atlasImage = Image.Create(AtlasW, AtlasH);
atlasImage.DrawCanvas(canvas =>
{
    using var typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold);
    using var font = new SKFont(typeface, CellSize * 0.7f);
    using var glyphPaint = new SKPaint
    {
        Color = new SKColor(0x10, 0x14, 0x1C),
        IsAntialias = true,
    };

    for (int row = 0; row < AtlasRows; row++)
    {
        for (int col = 0; col < AtlasCols; col++)
        {
            int idx = row * AtlasCols + col;
            var rect = new SKRect(
                col * CellSize, row * CellSize,
                (col + 1) * CellSize, (row + 1) * CellSize);

            using var bg = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateLinearGradient(
                    start: new SKPoint(rect.Left, rect.Top),
                    end:   new SKPoint(rect.Right, rect.Bottom),
                    colors: [hues[idx], hues[(idx + 5) % hues.Length]],
                    colorPos: [0f, 1f],
                    mode: SKShaderTileMode.Clamp),
            };
            // Rounded-rect cell with a small inset so the cells read
            // as discrete tiles when packed onto the framebuffer.
            canvas.DrawRoundRect(rect.InflateCopy(-4, -4), 12, 12, bg);

            var metrics = font.Metrics;
            float baselineY = (rect.Top + rect.Bottom) * 0.5f
                            - (metrics.Ascent + metrics.Descent) * 0.5f;
            canvas.DrawText(
                glyphs[idx].ToString(),
                (rect.Left + rect.Right) * 0.5f,
                baselineY,
                SKTextAlign.Center, font, glyphPaint);
        }
    }
});

// Wrap the image in an Atlas so glyph cells can be looked up by
// index instead of recomputing src rects on every blit. The Atlas
// takes ownership of the Image (default), so disposing the atlas
// releases everything.
var atlasGrid = Atlas.Grid(atlasImage, AtlasCols, AtlasRows);

// Confetti state. Each piece picks an atlas cell, a screen position,
// a velocity, a rotation rate, and a scale.
var rng = new Random(1234);
var confetti = new Confetti[ConfettiCount];
for (int i = 0; i < confetti.Length; i++)
    confetti[i] = SpawnConfetti(rng, initial: true);

var window = new Window2D(960, 540)
{
    Title = "Skia Bitmap (atlas blitting)",
    BackgroundColor = new Color(14, 16, 24),
    CloseKey = Key.Escape,
    AutoInvalidate = true,
};

// Tell the renderer to treat the surface as a fixed DesignW x DesignH
// drawing area and letterbox it into whatever the window grows to.
// All draws inside the rendering callback now use design coordinates;
// SDL handles scaling, centering, and letterbox bars (filled with the
// window's BackgroundColor).
window.Renderer.SetLogicalSize(DesignW, DesignH, LogicalPresentation.Letterbox);

window.Rendering += (w, rd) =>
{
    float dt = (float)rd.ElapsedSinceLastRender.TotalSeconds;

    // Show the atlas itself in the top-left so the source material is
    // obvious. Renders the whole image to a fixed-size destination.
    int previewSize = Math.Min(DesignW, DesignH) / 4;
    rd.DrawImage(atlasGrid.Image, new Rect(20, 20, previewSize, previewSize));

    // Caption -- pure Renderer2D, no Skia involved.
    rd.DrawColor = new Color(180, 190, 210);
    rd.DrawDebugText(20, 30 + previewSize, "atlas (one upload, many DrawImage blits)");

    // Per-frame confetti update: gravity + recycle when off-screen.
    for (int i = 0; i < confetti.Length; i++)
    {
        ref var c = ref confetti[i];
        c.Pos += c.Vel * dt;
        c.Vel.Y += 220f * dt;
        if (c.Pos.Y - c.Size > DesignH)
            c = SpawnConfetti(rng, initial: false);

        // Source rect comes from the Atlas; destination is a square at
        // the confetti's position. The atlas Image's GPU texture is
        // uploaded once and reused for every blit.
        var dst = new Rect(c.Pos.X - c.Size * 0.5f, c.Pos.Y - c.Size * 0.5f, c.Size, c.Size);
        atlasGrid.Draw(rd, c.AtlasIndex, dst);
    }
};

await window.WaitForCloseAsync();

static Confetti SpawnConfetti(Random rng, bool initial)
{
    float size = 18f + (float)rng.NextDouble() * 30f;
    return new Confetti
    {
        AtlasIndex = rng.Next(AtlasCols * AtlasRows),
        // On startup, scatter across the screen height so the field is
        // populated immediately; otherwise spawn just above the top
        // edge so recycled pieces fall in.
        Pos = new Vector2(
            (float)rng.NextDouble() * DesignW,
            initial
                ? (float)rng.NextDouble() * DesignH
                : -size - (float)rng.NextDouble() * 200f),
        Vel = new Vector2(
            ((float)rng.NextDouble() - 0.5f) * 80f,
            40f + (float)rng.NextDouble() * 120f),
        Size = size,
    };
}

struct Confetti
{
    public int AtlasIndex;
    public Vector2 Pos;
    public Vector2 Vel;
    public float Size;
}

static class SKRectExtensions
{
    public static SKRect InflateCopy(this SKRect r, float dx, float dy)
        => new(r.Left - dx, r.Top - dy, r.Right + dx, r.Bottom + dy);
}

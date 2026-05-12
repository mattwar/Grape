#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/ImageEffects.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Demonstrates the Skia-backed effect extensions on Image. Each
// effect returns a NEW image (the source is left untouched), so the
// pattern is: bake static effects once at startup, then blit the
// results normally each frame. The orbiting magnifier is the one
// exception -- it's rebuilt per frame because the lens position
// changes -- and shows the cost model honestly with `using`.

using Blitter;
using Blitter.Bits;

const int DesignW = 960;
const int DesignH = 540;

var window = new Window2D
{
    Title = "Image Effects (Skia)",
    BackgroundColor = new Color(18, 20, 28),
    FullScreen = true,
    CloseKey = Key.Escape,
    AutoAnimate = true,
};
window.Renderer.SetLogicalSize(DesignW, DesignH, LogicalPresentation.Letterbox);

using var source = Image.Load(Asset.GetPathRelativeToCaller("blitter.png"));

// Bake the static effects once. Each is a new image owning its own

// pixels; remember to dispose them (the `using` declarations do).
using var blurred    = source.Blur(6f);
using var shadowed   = source.DropShadow(8, 12, 6, 6, new Color(0, 0, 0, 200));
using var grayscaled = source.Grayscale();
using var tinted     = source.Tint(new Color(255, 110, 90));
using var dilated    = source.Dilate(3, 3);

// Layout: a 3x2 grid of cards. Each card draws its labeled effect
// centered in a fixed slot.
const int Cols = 3;
const int Rows = 2;
const int Margin = 24;
const int LabelHeight = 22;
int cardW = (DesignW - Margin * (Cols + 1)) / Cols;
int cardH = (DesignH - Margin * (Rows + 1) - 60) / Rows; // leave room for the magnifier strip

(string Label, Image Img)[] cards =
[
    ("original",    source),
    ("Blur(6)",     blurred),
    ("DropShadow",  shadowed),
    ("Grayscale",   grayscaled),
    ("Tint(red)",   tinted),
    ("Dilate(3)",   dilated),
];

window.Rendering += (w, rd) =>
{
    var t = rd.ElapsedSecondsSinceStart;

    // Draw the static-effect grid.
    for (int i = 0; i < cards.Length; i++)
    {
        int col = i % Cols;
        int row = i / Cols;
        int x = Margin + col * (cardW + Margin);
        int y = Margin + row * (cardH + Margin);

        // The DropShadow card needs a light backing -- a near-black
        // shadow on a near-black window background is invisible.
        if (cards[i].Label == "DropShadow")
        {
            rd.DrawColor = new Color(220, 225, 235);
            rd.DrawFillRect(new Rect(x, y, cardW, cardH - LabelHeight));
        }

        // Center the image inside the card, scaled to fit while
        // preserving aspect ratio.
        var (iw, ih) = cards[i].Img.Size;
        float fit = MathF.Min((float)cardW / iw, (float)(cardH - LabelHeight) / ih);
        float dw = iw * fit;
        float dh = ih * fit;
        float dx = x + (cardW - dw) * 0.5f;
        float dy = y + (cardH - LabelHeight - dh) * 0.5f;
        rd.DrawImage(cards[i].Img, new Rect(dx, dy, dw, dh));

        rd.DrawColor = new Color(200, 210, 230);
        rd.DrawDebugText(x + 4, y + cardH - LabelHeight + 4, cards[i].Label, scale: 1.5f);
    }

    // Animated magnifier strip across the bottom: a roaming lens
    // over the source image, demonstrating Magnify with nearest-
    // neighbor sampling for crisp pixel zoom. Built per frame --
    // dispose the result each frame so the GPU texture is released.
    int stripH = 50;
    int stripY = DesignH - stripH - 8;
    var (sw, sh) = source.Size;
    float lensSize = Math.Min(sw, sh) * 0.25f;
    float lx = (sw - lensSize) * 0.5f + (sw - lensSize) * 0.4f * MathF.Sin(t * 0.8f);
    float ly = (sh - lensSize) * 0.5f + (sh - lensSize) * 0.4f * MathF.Cos(t * 1.1f);
    var lens = new Rect(lx, ly, lensSize, lensSize);

    using (var magnified = source.Magnify(lens, zoom: 4f, inset: 6f, ImageSampling.Nearest))
    {
        // Draw the magnified result scaled to fit the strip height,
        // preserving aspect ratio.
        var (mw, mh) = magnified.Size;
        float mfit = (float)stripH / mh;
        float mdw = mw * mfit;
        rd.DrawImage(magnified, new Rect((DesignW - mdw) * 0.5f, stripY, mdw, stripH));
    }

    rd.DrawColor = new Color(160, 180, 220);
    rd.DrawDebugText(8, stripY - 16, "Magnify(lens, 4x, Nearest) -- rebuilt per frame", scale: 1.2f);
};

await window.WaitForCloseAsync();

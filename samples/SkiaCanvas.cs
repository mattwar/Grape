#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/SkiaCanvas.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// Demonstrates Renderer2D.DrawCanvas: hand the renderer a destination
// rect and a callback, draw with SkiaSharp's full vector / text /
// gradient / blur API, and the result composites onto the framebuffer
// alongside ordinary Renderer2D draws.
//
// The Skia scratch (SKBitmap + SKCanvas + backing Image) is pooled
// per-renderer, sized to the destination, and reused across frames --
// nothing is allocated per frame here.

using Blitter;
using SkiaSharp;

var window = new Window2D(960, 540)
{
    Title = "Skia Canvas",
    BackgroundColor = new Color(18, 22, 30),
    CloseKey = Key.Escape,
};

window.Rendering += (w, rd) =>
{
    var (width, height) = w.Size;
    var seconds = (float)rd.ElapsedSinceStart.TotalSeconds;

    // 1) Native Renderer2D draw underneath: a checkerboard of squares
    //    so the Skia overlays clearly composite on top of existing
    //    framebuffer content (and so the transparent regions of those
    //    overlays can be seen).
    DrawCheckerboard(rd, width, height, cell: 40);

    // Layout: top half split into two panels with a margin between
    // them; the bottom strip below holds the gauges. Everything is
    // computed from the live window size so resizing rearranges the
    // scene instead of clipping it.
    const int margin = 40;
    const int gap = 40;
    int topHeight = (int)(height * 0.52f) - margin - gap / 2;
    if (topHeight < 80) topHeight = 80;
    int leftWidth = (int)((width - margin * 2 - gap) * 0.45f);
    if (leftWidth < 120) leftWidth = 120;
    int rightWidth = width - margin * 2 - gap - leftWidth;
    if (rightWidth < 120) rightWidth = 120;

    // 2) Top-left panel: anti-aliased vector path, stroked with a
    //    radial gradient. The path animates so the AA quality is
    //    obvious in motion.
    var leftRect = new Rect(margin, margin, leftWidth, topHeight);
    rd.DrawCanvas(leftRect, canvas =>
    {
        DrawWaveRibbon(canvas, leftRect.Width, leftRect.Height, seconds);
    });

    // 3) Top-right panel: vector text with a soft drop shadow on a
    //    translucent rounded-rect background. Background is supplied
    //    as the DrawCanvas background color so the canvas starts
    //    pre-filled (one less Skia call in the user's callback).
    var rightRect = new Rect(margin + leftWidth + gap, margin, rightWidth, topHeight);
    rd.DrawCanvas(rightRect, new Color(0, 0, 0, 140), canvas =>
    {
        DrawHeadline(canvas, rightRect.Width, rightRect.Height);
    });

    // 4) Bottom strip: a row of pie-slice gauges, each its own
    //    DrawCanvas call. Each call reuses the same per-renderer
    //    scratch (resized when the slot's dimensions change), so the
    //    last one in the row is the size the scratch settles at for
    //    the next frame's first call. For uniform sizes this is a
    //    no-op resize.
    int bottomTop = margin + topHeight + gap;
    int bottomAvailable = height - bottomTop - margin;
    if (bottomAvailable < 60) bottomAvailable = 60;
    const int gaugeCount = 6;
    const int gaugeGap = 24;
    int gaugeSize = Math.Min(
        bottomAvailable,
        (width - margin * 2 - gaugeGap * (gaugeCount - 1)) / gaugeCount);
    if (gaugeSize < 40) gaugeSize = 40;
    int totalWidth = gaugeCount * gaugeSize + (gaugeCount - 1) * gaugeGap;
    int gaugeY = bottomTop + (bottomAvailable - gaugeSize) / 2;
    int gaugeX = (width - totalWidth) / 2;
    for (int i = 0; i < gaugeCount; i++)
    {
        var rect = new Rect(gaugeX + i * (gaugeSize + gaugeGap), gaugeY, gaugeSize, gaugeSize);
        float phase = seconds + i * 0.35f;
        rd.DrawCanvas(rect, canvas => DrawGauge(canvas, rect.Width, rect.Height, phase, i));
    }

    w.Invalidate(); // animate
};

await window.WaitForCloseAsync();

// --- Renderer2D draws (no Skia) -------------------------------------

static void DrawCheckerboard(Renderer2D rd, int width, int height, int cell)
{
    var darker  = new Color(24, 28, 38);
    var lighter = new Color(32, 38, 50);
    for (int y = 0; y < height; y += cell)
    {
        for (int x = 0; x < width; x += cell)
        {
            rd.DrawColor = ((x / cell + y / cell) & 1) == 0 ? darker : lighter;
            rd.DrawFillRect(new Rect(x, y, cell, cell));
        }
    }
}

// --- Skia draws (inside DrawCanvas callbacks) -----------------------

static void DrawWaveRibbon(SKCanvas canvas, float width, float height, float t)
{
    float pad = MathF.Max(8f, MathF.Min(width, height) * 0.05f);
    float labelSize = MathF.Max(10f, height * 0.05f);

    // Build an anti-aliased cubic path snaking across the panel.
    using var path = new SKPath();
    int samples = 64;
    float w = width - pad * 2;
    // Leave headroom at the bottom for the caption so the wave does
    // not overlap it as the panel shrinks.
    float waveBottom = height - pad - labelSize * 1.6f;
    float waveTop = pad;
    float waveCenter = (waveTop + waveBottom) * 0.5f;
    float waveAmplitude = (waveBottom - waveTop) * 0.5f;
    for (int i = 0; i <= samples; i++)
    {
        float u = i / (float)samples;
        float x = pad + u * w; 
        float y = waveCenter
                + MathF.Sin(u * MathF.Tau * 2 + t * 1.7f) * waveAmplitude * 0.65f
                + MathF.Sin(u * MathF.Tau * 0.7f - t * 1.1f) * waveAmplitude * 0.35f;
        if (i == 0) path.MoveTo(x, y);
        else path.LineTo(x, y);
    }

    // Radial gradient stroke -- the kind of fill SDL can't do natively.
    using var paint = new SKPaint
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = MathF.Max(2f, MathF.Min(width, height) * 0.025f),
        StrokeCap = SKStrokeCap.Round,
        IsAntialias = true,
        Shader = SKShader.CreateRadialGradient(
            center: new SKPoint(width * 0.5f, height * 0.5f),
            radius: width * 0.6f,
            colors: [new SKColor(0xFF, 0x6B, 0x9E), new SKColor(0x4E, 0x9C, 0xFF), new SKColor(0x6B, 0xFF, 0xC2)],
            colorPos: [0f, 0.55f, 1f],
            mode: SKShaderTileMode.Clamp),
    };
    canvas.DrawPath(path, paint);

    // Caption.
    using var labelPaint = new SKPaint { Color = new SKColor(0xE0, 0xE6, 0xF0), IsAntialias = true };
    using var labelFont  = new SKFont(SKTypeface.Default, labelSize);
    canvas.DrawText("anti-aliased path + radial gradient", pad, height - pad * 0.5f, SKTextAlign.Left, labelFont, labelPaint);
}

static void DrawHeadline(SKCanvas canvas, float width, float height)
{
    // Scale font sizes off the panel height so the headline fits
    // when the window is resized smaller, and grows when it isn't.
    float titleSize = MathF.Max(18f, height * 0.22f);
    float bodySize  = MathF.Max(10f, height * 0.085f);
    float pad       = MathF.Max(12f, height * 0.08f);
    float lineGap   = bodySize * 1.5f;

    // Big vector headline with a soft drop shadow.
    using var titleFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), titleSize);
    using var titlePaint = new SKPaint
    {
        Color = new SKColor(0xFF, 0xFF, 0xFF),
        IsAntialias = true,
        ImageFilter = SKImageFilter.CreateDropShadow(
            dx: 0, dy: 4, sigmaX: 4, sigmaY: 4,
            color: new SKColor(0, 0, 0, 200)),
    };
    float titleBaseline = pad + titleSize;
    canvas.DrawText("Skia inside Blitter", pad, titleBaseline, SKTextAlign.Left, titleFont, titlePaint);

    using var bodyFont  = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal), bodySize);
    using var bodyPaint = new SKPaint { Color = new SKColor(0xCB, 0xD4, 0xE6), IsAntialias = true };
    float y = titleBaseline + bodySize * 1.4f;
    canvas.DrawText("Vector text, drop shadows, gradients,",     pad, y,                 SKTextAlign.Left, bodyFont, bodyPaint);
    canvas.DrawText("paths and image filters via DrawCanvas --", pad, y + lineGap,       SKTextAlign.Left, bodyFont, bodyPaint);
    canvas.DrawText("composited onto the SDL framebuffer.",      pad, y + lineGap * 2,   SKTextAlign.Left, bodyFont, bodyPaint);

    // Thin underline that picks up Skia's AA.
    float ruleY = MathF.Min(y + lineGap * 2 + bodySize * 0.6f, height - pad * 0.5f);
    using var rule = new SKPaint { Color = new SKColor(0x6B, 0xFF, 0xC2), IsAntialias = true, StrokeWidth = 1f };
    canvas.DrawLine(pad, ruleY, width - pad, ruleY, rule);
}

static void DrawGauge(SKCanvas canvas, float width, float height, float phase, int index)
{
    var center = new SKPoint(width * 0.5f, height * 0.5f);
    float side = MathF.Min(width, height);
    float strokeWidth = MathF.Max(2f, side * 0.075f);
    // Inset the radius by half the stroke so the band stays inside
    // the gauge bounds at any size.
    float radius = side * 0.5f - strokeWidth * 0.5f - 2f;
    var bounds = new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);

    // Track.
    using var track = new SKPaint
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = strokeWidth,
        StrokeCap = SKStrokeCap.Round,
        IsAntialias = true,
        Color = new SKColor(0xFF, 0xFF, 0xFF, 40),
    };
    canvas.DrawCircle(center, radius, track);

    // Sweeping arc whose color cycles per gauge.
    float sweep = (MathF.Sin(phase) * 0.5f + 0.5f) * 320f + 20f; // 20..340 deg
    var hue = (index * 51 + phase * 18f) % 360f;
    using var arcPaint = new SKPaint
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = strokeWidth,
        StrokeCap = SKStrokeCap.Round,
        IsAntialias = true,
        Color = SKColor.FromHsv(hue, 70, 100),
    };
    using var arc = new SKPath();
    arc.AddArc(bounds, startAngle: -90f, sweepAngle: sweep);
    canvas.DrawPath(arc, arcPaint);

    // Centered value label, sized off the gauge.
    int value = (int)sweep;
    float fontSize = MathF.Max(10f, side * 0.22f);
    using var labelFont  = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), fontSize);
    using var labelPaint = new SKPaint { Color = new SKColor(0xF0, 0xF4, 0xFF), IsAntialias = true };
    // Vertically center using font metrics so the digits sit on the
    // gauge centerline regardless of font size.
    var metrics = labelFont.Metrics;
    float baselineY = center.Y - (metrics.Ascent + metrics.Descent) * 0.5f;
    canvas.DrawText(value.ToString(), center.X, baselineY, SKTextAlign.Center, labelFont, labelPaint);
}

#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/Logo.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// Renders the Blitter logo
using System.Numerics;
using Blitter;

// --- Layout knobs ---------------------------------------------------

const int CellSize = 32;     // size of one bit-cell, in device pixels
const int CellGap  = 4;      // transparent gap between adjacent cells
const int GridSize = 8;      // block is GridSize x GridSize cells

// Asymmetric padding -- the breaking corner is top-right and bits
// tumble down-and-right, so the canvas needs extra room on the right
// and bottom while the top can stay tight.
const int PadLeft   = 2;
const int PadRight  = 4;
const int PadTop    = 2;
const int PadBottom = 4;

const bool SavePng = true;   // write Logo.png next to the binary on launch

// Total canvas size in cells.
const int CanvasW = GridSize + PadLeft + PadRight;
const int CanvasH = GridSize + PadTop  + PadBottom;

// --- Palette --------------------------------------------------------

// Two-group palette: cool blues for the block "background" and warmer
// violets/rose for the foreground letterform. Within each group the
// shades are scattered to give the stacked-bit look without becoming
// confetti.
var bgPalette = new[]
{
    new Color(0x6E, 0x8F, 0xB8), // 0 dusty blue   (dominant bg)
    new Color(0xA8, 0xB8, 0xE0), // 1 periwinkle
    new Color(0x86, 0xAE, 0x9C), // 2 sage green   (cool accent)
};

var fgPalette = new[]
{
    new Color(0x6B, 0x48, 0x82), // 0 deep violet   (dominant fg)
    new Color(0x82, 0x5C, 0x9C), // 1 medium violet (subtle variance)
    new Color(0x99, 0x76, 0xB2), // 2 light violet  (rare highlight)
};

// Foreground "B" shape on an 8x8 grid. Cell coords are block-local.
// The B is inset by one cell on top and bottom (rows 0 and 7 stay
// background) and sits left of center so it doesn't intersect the
// breaking corner at the top-right.
var foreground = new HashSet<(int X, int Y)>
{
    // top bar         row 1
    (2,1), (3,1), (4,1),
    // upper bowl side row 2
    (2,2),               (5,2),
    // middle bar      row 3
    (2,3), (3,3), (4,3),
    // lower bowl side rows 4-5
    (2,4),               (5,4),
    (2,5),               (5,5),
    // bottom bar      row 6
    (2,6), (3,6), (4,6),
};

// Pick a palette index within a group. Deterministic hash gives a
// pleasing scatter; index 0 strongly dominates so each region reads
// as one coherent tone with subtle variance rather than a confetti.
static int ColorIndex(int x, int y, int paletteSize)
{
    var h = (x * 73 + y * 131 + 17) & 0xFF;
    // ~70% index 0, ~24% index 1, ~6% index 2 (when paletteSize == 3)
    if (paletteSize <= 1) return 0;
    if (paletteSize == 2) return h < 190 ? 0 : 1;
    return h switch
    {
        < 180 => 0,
        < 240 => 1,
        _     => 2,
    };
}

// --- Geometry -------------------------------------------------------

// Cells removed from the block (block-local coords; top-right corner
// crumbling). Stepped notch reads as "the corner came loose" rather
// than a clean bite.
var missing = new HashSet<(int X, int Y)>
{
    (7, 0),
    (6, 0),
    (7, 1),
};

// Falling bits: each tumbles off the breaking corner. Coords are in
// canvas cells (so they sit relative to the whole image, independent
// of block padding). Bits closer to the corner stay larger; the arc
// pitches down-and-right rather than blowing horizontally away. The
// breaking corner is in the bg region, so falling bits use bgPalette.
var falling = new (float Cx, float Cy, float Size, float AngleDeg, int ColorIdx)[]
{
    (PadLeft + 8.3f, PadTop + 0.2f, 0.95f,  18f, 1), // periwinkle
    (PadLeft + 9.0f, PadTop + 1.4f, 0.85f, -24f, 0), // dusty blue
    (PadLeft + 9.7f, PadTop + 2.8f, 0.75f,  36f, 2), // sage
};

// Block-edge cells that look tilted -- the bottom-left pair are
// settling into place from outside; the top-right pair are loose
// neighbors of the breaking corner, about to follow it off. Coords
// are block-local; each entry is (cellX, cellY, angleDeg).
var tiltedEdge = new (int X, int Y, float AngleDeg)[]
{
    (0, 4,  -8f),   // left edge: tipping in
    (3, 7,  12f),   // bottom edge: settling forward
    (5, 0,  18f),   // top-right: tipping toward the breaking corner
    (6, 1, -22f),   // top-right: tipping away with the corner
};

// --- Rendering helpers ---------------------------------------------

// Draw an axis-aligned bit-cell at (cellX, cellY) in canvas-cell
// coords. The drawn rect is shrunk by CellGap to leave transparent
// separation between adjacent bits.
static void DrawCell(Renderer2D rd, float cellX, float cellY)
{
    var px = cellX * CellSize + CellGap * 0.5f;
    var py = cellY * CellSize + CellGap * 0.5f;
    var s  = CellSize - CellGap;
    rd.DrawFillRect(new Rect(px, py, s, s));
}

// Draw a rotated square as two triangles via DrawGeometry. Used for
// the tumbling falling bits so they don't snap to the pixel grid.
static void DrawRotatedCell(Renderer2D rd, float centerX, float centerY,
                            float size, float angleRad, Color color)
{
    var half = (size - CellGap) * 0.5f;
    var cos = MathF.Cos(angleRad);
    var sin = MathF.Sin(angleRad);

    Vector2 Corner(float lx, float ly) => new(
        centerX + lx * cos - ly * sin,
        centerY + lx * sin + ly * cos);

    Span<Vertex2D> verts = stackalloc Vertex2D[]
    {
        new(Corner(-half, -half), color),
        new(Corner( half, -half), color),
        new(Corner( half,  half), color),
        new(Corner(-half,  half), color),
    };
    Span<int> idx = stackalloc[] { 0, 1, 2, 0, 2, 3 };
    rd.DrawGeometry(verts, idx);
}

void DrawLogo(Renderer2D rd)
{
    // Block: every cell is colored. Cells in `foreground` use the fg
    // palette (forming the letter B) -- always solid index 0 so the
    // letter reads cleanly. Background cells get the per-cell mix.
    for (int gy = 0; gy < GridSize; gy++)
    for (int gx = 0; gx < GridSize; gx++)
    {
        if (missing.Contains((gx, gy)))
            continue;
        if (tiltedEdge.Any(t => t.X == gx && t.Y == gy))
            continue; // drawn rotated below

        if (foreground.Contains((gx, gy)))
        {
            rd.DrawColor = fgPalette[0]; // solid dark violet for the letterform
        }
        else
        {
            rd.DrawColor = bgPalette[ColorIndex(gx, gy, bgPalette.Length)];
        }
        DrawCell(rd, PadLeft + gx, PadTop + gy);
    }

    // Tilted edge bits: rendered slightly rotated about their cell
    // center so they read as bits about to lock into the block.
    foreach (var (gx, gy, angleDeg) in tiltedEdge)
    {
        var color = foreground.Contains((gx, gy))
            ? fgPalette[0]
            : bgPalette[ColorIndex(gx, gy, bgPalette.Length)];
        var centerX = (PadLeft + gx + 0.5f) * CellSize;
        var centerY = (PadTop  + gy + 0.5f) * CellSize;
        DrawRotatedCell(rd, centerX, centerY, CellSize,
            angleDeg * MathF.PI / 180f, color);
    }

    // Falling bits: tumble off the breaking corner, arcing down.
    foreach (var (cx, cy, size, angleDeg, colorIdx) in falling)
    {
        var centerX = (cx + 0.5f) * CellSize;
        var centerY = (cy + 0.5f) * CellSize;
        var pixelSize = size * CellSize;
        DrawRotatedCell(rd, centerX, centerY, pixelSize,
            angleDeg * MathF.PI / 180f, bgPalette[colorIdx]);
    }
}

if (SavePng)
{
    using var image = Bitmap.Create(
        CanvasW * CellSize, CanvasH * CellSize, PixelFormat.ABGR8888);

    // Transparent background so the saved PNG drops cleanly onto any
    // backdrop (NuGet listing, README, dark/light docs, etc.).
    image.Render2D(new Color(0, 0, 0, 0), DrawLogo);

    var path = Path.Combine(AppContext.BaseDirectory, "Logo.png");
    image.Save(path);
    Console.WriteLine($"Saved: {path}");
}

// --- On-screen preview ---------------------------------------------

var window = new Window2D(CanvasW * CellSize, CanvasH * CellSize)
{
    Title = "Blitter logo",
    BackgroundColor = new Color(0x18, 0x1C, 0x24),
    CloseKey = Key.Escape,
};

window.Rendering += (_, rd) => DrawLogo(rd);

await window.WaitForCloseAsync();

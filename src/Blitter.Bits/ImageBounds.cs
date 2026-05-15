using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Helpers to compute bounds for an <see cref="Image"/>.
/// </summary>
public static class ImageBounds
{
    /// <summary>
    /// Gets the minimum axis-aligned bounding rectangle that contains every pixel.
    /// </summary>
    public static BoundingRect ComputeOpaqueBounds(this Bitmap image, byte alphaThreshold = 0)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (w, h) = image.Size;
        if (w <= 0 || h <= 0) return BoundingRect.Empty;

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        // Single pass tracking min/max of opaque pixel coords.
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (image.GetPixel(x, y).A > alphaThreshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (minX == int.MaxValue) return BoundingRect.Empty;

        // Half-open [Min, Max): a single opaque pixel at (5,5) yields
        // Min=(5,5), Max=(6,6) -- size 1x1.
        return new BoundingRect(
            new Vector2(minX, minY),
            new Vector2(maxX + 1, maxY + 1));
    }

    /// <summary>
    /// Gets the minimal axis-aligned bounding circle that contains every pixel.
    /// </summary>
    public static BoundingCircle ComputeOpaqueCircle(this Bitmap image, byte alphaThreshold = 0) =>
        BoundingCircle.FromRect(image.ComputeOpaqueBounds(alphaThreshold));

    /// <summary>
    /// Computes a nominal set of axis-aligned bounding rectangles that cover every pixel.
    /// </summary>
    public static BoundingRect[] ComputeOpaqueRects(
        this Bitmap image,
        int cellSize = 8,
        byte alphaThreshold = 0)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfLessThan(cellSize, 1);

        var (w, h) = image.Size;
        if (w <= 0 || h <= 0) return Array.Empty<BoundingRect>();

        int cols = (w + cellSize - 1) / cellSize;
        int rows = (h + cellSize - 1) / cellSize;

        // opaque[r * cols + c] is true if any pixel in cell (c, r) exceeds
        // the alpha threshold.
        var opaque = new bool[rows * cols];
        bool any = false;
        for (int y = 0; y < h; y++)
        {
            int r = y / cellSize;
            int rowBase = r * cols;
            for (int x = 0; x < w; x++)
            {
                if (image.GetPixel(x, y).A > alphaThreshold)
                {
                    int idx = rowBase + (x / cellSize);
                    if (!opaque[idx])
                    {
                        opaque[idx] = true;
                        any = true;
                    }
                }
            }
        }
        if (!any) return Array.Empty<BoundingRect>();

        // Greedy rectangle merge: scan cells row-major; for each opaque
        // cell not yet consumed, expand right while cells stay opaque,
        // then expand down while every cell across that horizontal span
        // stays opaque. Mark consumed cells and emit the rectangle.
        var consumed = new bool[opaque.Length];
        var rects = new List<BoundingRect>();
        for (int r = 0; r < rows; r++)
        {
            int rowBase = r * cols;
            for (int c = 0; c < cols; c++)
            {
                int idx = rowBase + c;
                if (!opaque[idx] || consumed[idx]) continue;

                int c1 = c;
                while (c1 + 1 < cols
                       && opaque[rowBase + c1 + 1]
                       && !consumed[rowBase + c1 + 1])
                {
                    c1++;
                }

                int r1 = r;
                while (r1 + 1 < rows && CanExtendDown(opaque, consumed, cols, c, c1, r1 + 1))
                {
                    r1++;
                }

                for (int rr = r; rr <= r1; rr++)
                {
                    int rb = rr * cols;
                    for (int cc = c; cc <= c1; cc++)
                        consumed[rb + cc] = true;
                }

                int minPx = c * cellSize;
                int minPy = r * cellSize;
                int maxPx = Math.Min((c1 + 1) * cellSize, w);
                int maxPy = Math.Min((r1 + 1) * cellSize, h);
                rects.Add(new BoundingRect(
                    new Vector2(minPx, minPy),
                    new Vector2(maxPx, maxPy)));
            }
        }

        return rects.ToArray();
    }

    private static bool CanExtendDown(bool[] opaque, bool[] consumed, int cols, int c0, int c1, int r)
    {
        int rb = r * cols;
        for (int c = c0; c <= c1; c++)
        {
            int i = rb + c;
            if (!opaque[i] || consumed[i]) return false;
        }
        return true;
    }
}

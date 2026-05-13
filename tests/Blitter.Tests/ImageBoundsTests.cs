using System.Numerics;
using Blitter.Bits;

namespace Blitter.Tests;

public class ImageBoundsTests
{
    private static BitmapImage MakeImage(int w, int h, Action<BitmapImage> draw)
    {
        var img = Image.Create(w, h);
        draw(img);
        return img;
    }

    [Fact]
    public void ComputeOpaqueBounds_AllTransparent_ReturnsEmpty()
    {
        using var img = MakeImage(8, 8, _ => { /* leave clear */ });
        var b = img.ComputeOpaqueBounds();
        Assert.True(b.IsEmpty);
    }

    [Fact]
    public void ComputeOpaqueBounds_SingleOpaquePixel_TightOneByOne()
    {
        using var img = MakeImage(16, 16, i => i.SetPixel(5, 7, Color.White));
        var b = img.ComputeOpaqueBounds();
        Assert.Equal(new Vector2(5, 7), b.Min);
        Assert.Equal(new Vector2(6, 8), b.Max); // half-open
        Assert.Equal(new Vector2(1, 1), b.Size);
    }

    [Fact]
    public void ComputeOpaqueBounds_RectangularBlock_ReturnsBlockBounds()
    {
        using var img = MakeImage(32, 32, i =>
        {
            for (int y = 4; y <= 10; y++)
                for (int x = 6; x <= 12; x++)
                    i.SetPixel(x, y, Color.White);
        });
        var b = img.ComputeOpaqueBounds();
        Assert.Equal(new Vector2(6, 4), b.Min);
        Assert.Equal(new Vector2(13, 11), b.Max);
        Assert.Equal(new Vector2(7, 7), b.Size);
    }

    [Fact]
    public void ComputeOpaqueBounds_ThresholdIgnoresFringe()
    {
        using var img = MakeImage(16, 16, i =>
        {
            i.SetPixel(2, 2, Color.White);                         // alpha 255
            i.SetPixel(10, 10, new Color(255, 255, 255, 4));      // alpha 4 (fringe)
        });

        // Default threshold 0: includes the fringe pixel.
        var loose = img.ComputeOpaqueBounds();
        Assert.Equal(new Vector2(2, 2), loose.Min);
        Assert.Equal(new Vector2(11, 11), loose.Max);

        // Threshold 8: rejects the fringe.
        var tight = img.ComputeOpaqueBounds(alphaThreshold: 8);
        Assert.Equal(new Vector2(2, 2), tight.Min);
        Assert.Equal(new Vector2(3, 3), tight.Max);
    }

    [Fact]
    public void ComputeOpaqueBounds_FullyOpaqueImage_FullSize()
    {
        using var img = MakeImage(4, 3, i =>
        {
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 4; x++)
                    i.SetPixel(x, y, Color.White);
        });
        var b = img.ComputeOpaqueBounds();
        Assert.Equal(new Vector2(0, 0), b.Min);
        Assert.Equal(new Vector2(4, 3), b.Max);
    }

    [Fact]
    public void ComputeOpaqueBounds_NullImage_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((BitmapImage)null!).ComputeOpaqueBounds());
    }

    [Fact]
    public void ComputeOpaqueCircle_MatchesFromRectOfBounds()
    {
        using var img = MakeImage(32, 32, i =>
        {
            for (int y = 4; y <= 10; y++)
                for (int x = 6; x <= 12; x++)
                    i.SetPixel(x, y, Color.White);
        });
        var expected = BoundingCircle.FromRect(img.ComputeOpaqueBounds());
        var actual = img.ComputeOpaqueCircle();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ComputeOpaqueCircle_AllTransparent_ReturnsEmpty()
    {
        using var img = MakeImage(8, 8, _ => { });
        Assert.True(img.ComputeOpaqueCircle().IsEmpty);
    }

    // ---------------- ComputeOpaqueRects ----------------

    [Fact]
    public void ComputeOpaqueRects_AllTransparent_ReturnsEmpty()
    {
        using var img = MakeImage(16, 16, _ => { });
        Assert.Empty(img.ComputeOpaqueRects());
    }

    [Fact]
    public void ComputeOpaqueRects_SingleBlock_ReturnsOneRect()
    {
        using var img = MakeImage(32, 32, i =>
        {
            for (int y = 8; y < 16; y++)
                for (int x = 8; x < 16; x++)
                    i.SetPixel(x, y, Color.White);
        });

        var rects = img.ComputeOpaqueRects(cellSize: 8);
        Assert.Single(rects);
        Assert.Equal(new Vector2(8, 8), rects[0].Min);
        Assert.Equal(new Vector2(16, 16), rects[0].Max);
    }

    [Fact]
    public void ComputeOpaqueRects_TwoSeparateBlocks_ReturnsTwoRects()
    {
        // Two 8x8 opaque blocks separated by an 8-pixel gap of transparency.
        using var img = MakeImage(32, 8, i =>
        {
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++) i.SetPixel(x, y, Color.White);
                for (int x = 16; x < 24; x++) i.SetPixel(x, y, Color.White);
            }
        });

        var rects = img.ComputeOpaqueRects(cellSize: 8);
        Assert.Equal(2, rects.Length);
        Assert.Contains(rects, r => r.Min == new Vector2(0, 0) && r.Max == new Vector2(8, 8));
        Assert.Contains(rects, r => r.Min == new Vector2(16, 0) && r.Max == new Vector2(24, 8));
    }

    [Fact]
    public void ComputeOpaqueRects_FullImage_OneRectClampedToImage()
    {
        using var img = MakeImage(10, 6, i =>
        {
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 10; x++)
                    i.SetPixel(x, y, Color.White);
        });

        var rects = img.ComputeOpaqueRects(cellSize: 8);
        Assert.Single(rects);
        Assert.Equal(new Vector2(0, 0), rects[0].Min);
        Assert.Equal(new Vector2(10, 6), rects[0].Max);
    }

    [Fact]
    public void ComputeOpaqueRects_LShape_ProducesTwoMergedRects()
    {
        // L-shape: 16x16 vertical bar on the left + 16x8 bottom extension.
        // With cellSize=8 the grid is 4x3 cells; greedy merge produces
        // a single 1x3 column on the left first (consuming the corner),
        // then a 2x1 strip for the remaining bottom-right cells. Two rects.
        using var img = MakeImage(32, 24, i =>
        {
            for (int y = 0; y < 24; y++)
                for (int x = 0; x < 8; x++)
                    i.SetPixel(x, y, Color.White);
            for (int y = 16; y < 24; y++)
                for (int x = 0; x < 24; x++)
                    i.SetPixel(x, y, Color.White);
        });

        var rects = img.ComputeOpaqueRects(cellSize: 8);
        Assert.Equal(2, rects.Length);
    }

    [Fact]
    public void ComputeOpaqueRects_CellsizeOne_OneRectPerOpaquePixelMerged()
    {
        using var img = MakeImage(4, 4, i =>
        {
            i.SetPixel(0, 0, Color.White);
            i.SetPixel(3, 3, Color.White);
        });

        var rects = img.ComputeOpaqueRects(cellSize: 1);
        Assert.Equal(2, rects.Length);
    }

    [Fact]
    public void ComputeOpaqueRects_InvalidCellSize_Throws()
    {
        using var img = MakeImage(4, 4, _ => { });
        Assert.Throws<ArgumentOutOfRangeException>(() => img.ComputeOpaqueRects(cellSize: 0));
    }
}

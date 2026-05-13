using Blitter.Bits;

namespace Blitter.Tests;

public class AtlasTests
{
    private static BitmapImage CreateImage(int w = 16, int h = 16) =>
        Image.Create(w, h, PixelFormat.RGBA8888);

    [Fact]
    public void Construct_StoresImageAndRects()
    {
        var image = CreateImage();
        var rects = new[] { new Rect(0, 0, 8, 8), new Rect(8, 0, 8, 8) };
        using var atlas = new Atlas(image, rects);

        Assert.Same(image, atlas.Image);
        Assert.Equal(2, atlas.Count);
        Assert.Equal(rects[0], atlas[0]);
        Assert.Equal(rects[1], atlas[1]);
    }

    [Fact]
    public void Indexer_OutOfRange_Throws()
    {
        using var atlas = new Atlas(CreateImage(), [new Rect(0, 0, 4, 4)]);
        Assert.Throws<IndexOutOfRangeException>(() => atlas[1]);
    }

    [Fact]
    public void NameLookup_ReturnsRect()
    {
        var rects = new[] { new Rect(0, 0, 4, 4), new Rect(4, 0, 4, 4) };
        var names = new Dictionary<string, int> { ["alpha"] = 0, ["beta"] = 1 };
        using var atlas = new Atlas(CreateImage(), rects, names);

        Assert.Equal(rects[0], atlas["alpha"]);
        Assert.Equal(rects[1], atlas["beta"]);
        Assert.True(atlas.Contains("alpha"));
        Assert.False(atlas.Contains("missing"));
    }

    [Fact]
    public void NameLookup_Missing_Throws()
    {
        var names = new Dictionary<string, int> { ["a"] = 0 };
        using var atlas = new Atlas(CreateImage(), [new Rect(0, 0, 4, 4)], names);
        Assert.Throws<KeyNotFoundException>(() => atlas["nope"]);
    }

    [Fact]
    public void NameLookup_WithoutMap_Throws()
    {
        using var atlas = new Atlas(CreateImage(), [new Rect(0, 0, 4, 4)]);
        Assert.Throws<InvalidOperationException>(() => atlas["x"]);
    }

    [Fact]
    public void TryGetIndex_Resolves()
    {
        var names = new Dictionary<string, int> { ["a"] = 0, ["b"] = 1 };
        using var atlas = new Atlas(
            CreateImage(),
            [new Rect(0, 0, 4, 4), new Rect(4, 0, 4, 4)],
            names);

        Assert.True(atlas.TryGetIndex("b", out var i));
        Assert.Equal(1, i);
        Assert.False(atlas.TryGetIndex("missing", out var j));
        Assert.Equal(-1, j);
    }

    [Fact]
    public void NamesValidated_AgainstRectCount()
    {
        var names = new Dictionary<string, int> { ["a"] = 5 };
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Atlas(CreateImage(), [new Rect(0, 0, 4, 4)], names));
    }

    [Fact]
    public void Grid_DerivesCellsFromImageSize()
    {
        // 16x16 image, 4x4 grid -> 4x4 cells
        var image = CreateImage(16, 16);
        using var atlas = Atlas.Grid(image, columns: 4, rows: 4);

        Assert.Equal(16, atlas.Count);
        Assert.Equal(new Rect(0, 0, 4, 4), atlas[0]);
        Assert.Equal(new Rect(4, 0, 4, 4), atlas[1]);
        Assert.Equal(new Rect(0, 4, 4, 4), atlas[4]);     // row 1, col 0
        Assert.Equal(new Rect(12, 12, 4, 4), atlas[15]);  // row 3, col 3
    }

    [Fact]
    public void Grid_RowMajor()
    {
        // Index = row * columns + col
        using var atlas = Atlas.Grid(CreateImage(12, 8), columns: 3, rows: 2);
        // cellW = 12/3 = 4, cellH = 8/2 = 4
        Assert.Equal(new Rect(8, 4, 4, 4), atlas[5]); // row 1, col 2
    }

    [Fact]
    public void Grid_WithExplicitCellSize_UsesIt()
    {
        // 20x20 image but only the top-left 16x16 is meaningful (4x4 cells of 4px)
        using var atlas = Atlas.Grid(CreateImage(20, 20),
            columns: 4, rows: 4, cellWidth: 4, cellHeight: 4);

        Assert.Equal(16, atlas.Count);
        Assert.Equal(new Rect(12, 12, 4, 4), atlas[15]);
    }

    [Fact]
    public void Grid_InvalidArgs_Throws()
    {
        var img = CreateImage();
        Assert.Throws<ArgumentOutOfRangeException>(() => Atlas.Grid(img, 0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => Atlas.Grid(img, 2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Atlas.Grid(img, 2, 2, 0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => Atlas.Grid(img, 2, 2, 4, 0));
    }

    [Fact]
    public void Dispose_DefaultOwnsImage()
    {
        var image = CreateImage();
        var atlas = new Atlas(image, [new Rect(0, 0, 4, 4)]);
        atlas.Dispose();
        Assert.True(image.IsDisposed);
    }

    [Fact]
    public void Dispose_DoesNotDisposeImageWhenNotOwned()
    {
        var image = CreateImage();
        var atlas = new Atlas(image, [new Rect(0, 0, 4, 4)], ownsImage: false);
        atlas.Dispose();
        Assert.False(image.IsDisposed);
        image.Dispose();
    }

    [Fact]
    public void Dispose_Idempotent()
    {
        var image = CreateImage();
        var atlas = new Atlas(image, [new Rect(0, 0, 4, 4)]);
        atlas.Dispose();
        atlas.Dispose(); // does not throw
    }
}

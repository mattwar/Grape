namespace Grape.Tests;

public class ImageTransformTests
{
    // Build a tiny 3x2 image where each pixel is a unique recognisable
    // color. Channels are spread across the byte range so any axis swap
    // or off-by-one in the transform shows up as a wrong color.
    //
    //   src layout (x,y) -> color:
    //   (0,0) red       (1,0) green      (2,0) blue
    //   (0,1) yellow    (1,1) magenta    (2,1) cyan
    private static (Image image, Color[,] expected) BuildSample()
    {
        var img = Image.Create(3, 2, PixelFormat.RGBA8888);
        var grid = new Color[3, 2];
        grid[0, 0] = new Color(255, 0, 0);
        grid[1, 0] = new Color(0, 255, 0);
        grid[2, 0] = new Color(0, 0, 255);
        grid[0, 1] = new Color(255, 255, 0);
        grid[1, 1] = new Color(255, 0, 255);
        grid[2, 1] = new Color(0, 255, 255);
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 3; x++)
                img.SetPixel(x, y, grid[x, y]);
        return (img, grid);
    }

    [Fact]
    public void Flip_None_ReturnsIdenticalCopy()
    {
        var (src, grid) = BuildSample();
        using var result = src.Flip(FlipMode.None);
        Assert.Equal(src.Size, result.Size);
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 3; x++)
                Assert.Equal(grid[x, y], result.GetPixel(x, y));
    }

    [Fact]
    public void Flip_Horizontal_MirrorsLeftToRight()
    {
        var (src, grid) = BuildSample();
        using var result = src.Flip(FlipMode.Horizontal);
        Assert.Equal(src.Size, result.Size);
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 3; x++)
                Assert.Equal(grid[2 - x, y], result.GetPixel(x, y));
    }

    [Fact]
    public void Flip_Vertical_MirrorsTopToBottom()
    {
        var (src, grid) = BuildSample();
        using var result = src.Flip(FlipMode.Vertical);
        Assert.Equal(src.Size, result.Size);
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 3; x++)
                Assert.Equal(grid[x, 1 - y], result.GetPixel(x, y));
    }

    [Fact]
    public void Rotate_Clockwise90_SwapsDimensions()
    {
        var (src, _) = BuildSample();
        using var result = src.Rotate(Rotation.Clockwise90);
        Assert.Equal((2, 3), result.Size);
    }

    [Fact]
    public void Rotate_Clockwise90_TopLeftMovesToTopRight()
    {
        var (src, grid) = BuildSample();
        using var result = src.Rotate(Rotation.Clockwise90);
        // src (0,0) -> dst (resultWidth-1, 0)
        Assert.Equal(grid[0, 0], result.GetPixel(1, 0));
        // src (2,0) -> dst (resultWidth-1, resultHeight-1)
        Assert.Equal(grid[2, 0], result.GetPixel(1, 2));
        // src (0,1) -> dst (0, 0)
        Assert.Equal(grid[0, 1], result.GetPixel(0, 0));
        // src (2,1) -> dst (0, resultHeight-1)
        Assert.Equal(grid[2, 1], result.GetPixel(0, 2));
    }

    [Fact]
    public void Rotate_Counterclockwise90_TopLeftMovesToBottomLeft()
    {
        var (src, grid) = BuildSample();
        using var result = src.Rotate(Rotation.Counterclockwise90);
        Assert.Equal((2, 3), result.Size);
        // src (0,0) -> dst (0, resultHeight-1)
        Assert.Equal(grid[0, 0], result.GetPixel(0, 2));
        // src (2,0) -> dst (0, 0)
        Assert.Equal(grid[2, 0], result.GetPixel(0, 0));
        // src (0,1) -> dst (resultWidth-1, resultHeight-1)
        Assert.Equal(grid[0, 1], result.GetPixel(1, 2));
        // src (2,1) -> dst (resultWidth-1, 0)
        Assert.Equal(grid[2, 1], result.GetPixel(1, 0));
    }

    [Fact]
    public void Rotate_Half_KeepsDimensionsAndInvertsBothAxes()
    {
        var (src, grid) = BuildSample();
        using var result = src.Rotate(Rotation.Half);
        Assert.Equal(src.Size, result.Size);
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 3; x++)
                Assert.Equal(grid[2 - x, 1 - y], result.GetPixel(x, y));
    }

    [Fact]
    public void Rotate_FourClockwise90_ReturnsToOriginal()
    {
        var (src, grid) = BuildSample();
        using var step1 = src.Rotate(Rotation.Clockwise90);
        using var step2 = step1.Rotate(Rotation.Clockwise90);
        using var step3 = step2.Rotate(Rotation.Clockwise90);
        using var step4 = step3.Rotate(Rotation.Clockwise90);
        Assert.Equal(src.Size, step4.Size);
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 3; x++)
                Assert.Equal(grid[x, y], step4.GetPixel(x, y));
    }

    [Fact]
    public void Transform_PreservesMipmapsFlag()
    {
        var src = Image.Create(2, 2, PixelFormat.RGBA8888, mipmaps: true);
        using var flipped = src.Flip(FlipMode.Horizontal);
        using var rotated = src.Rotate(Rotation.Clockwise90);
        Assert.True(flipped.Mipmaps);
        Assert.True(rotated.Mipmaps);
    }
}

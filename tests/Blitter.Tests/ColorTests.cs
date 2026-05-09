using System.Numerics;

namespace Blitter.Tests;

public class ColorTests
{
    [Fact]
    public void Constructor_DefaultsAlphaTo255()
    {
        var c = new Color(10, 20, 30);
        Assert.Equal(255, c.A);
    }

    [Fact]
    public void FromRgba_StoresAllChannels()
    {
        var c = Color.FromRgba(1, 2, 3, 4);
        Assert.Equal((byte)1, c.R);
        Assert.Equal((byte)2, c.G);
        Assert.Equal((byte)3, c.B);
        Assert.Equal((byte)4, c.A);
    }

    [Fact]
    public void WithAlpha_ReplacesOnlyAlpha()
    {
        var c = new Color(10, 20, 30, 40).WithAlpha(99);
        Assert.Equal(new Color(10, 20, 30, 99), c);
    }

    [Fact]
    public void Deconstruct_YieldsAllChannels()
    {
        var (r, g, b, a) = new Color(5, 6, 7, 8);
        Assert.Equal((5, 6, 7, 8), ((int)r, (int)g, (int)b, (int)a));
    }

    [Fact]
    public void Equality_IsByValue()
    {
        Assert.Equal(new Color(1, 2, 3, 4), new Color(1, 2, 3, 4));
        Assert.NotEqual(new Color(1, 2, 3, 4), new Color(1, 2, 3, 5));
        Assert.True(new Color(1, 2, 3, 4) == new Color(1, 2, 3, 4));
        Assert.True(new Color(1, 2, 3, 4) != new Color(9, 2, 3, 4));
    }

    [Fact]
    public void ImplicitToVector4_NormalizesChannels()
    {
        Vector4 v = new Color(255, 0, 128, 255);
        Assert.Equal(1f, v.X, 5);
        Assert.Equal(0f, v.Y, 5);
        Assert.Equal(128f / 255f, v.Z, 5);
        Assert.Equal(1f, v.W, 5);
    }

    [Theory]
    [InlineData(0f, 1f, 1f, 255, 0, 0)]    // red
    [InlineData(1f / 3f, 1f, 1f, 0, 255, 0)] // green
    [InlineData(2f / 3f, 1f, 1f, 0, 0, 255)] // blue
    [InlineData(0f, 0f, 0f, 0, 0, 0)]      // black
    [InlineData(0f, 0f, 1f, 255, 255, 255)] // white
    public void FromHsv_PrimariesAndExtremes(float h, float s, float v, int r, int g, int b)
    {
        var c = Color.FromHsv(h, s, v);
        Assert.Equal((byte)r, c.R);
        Assert.Equal((byte)g, c.G);
        Assert.Equal((byte)b, c.B);
        Assert.Equal((byte)255, c.A);
    }

    [Fact]
    public void FromHsv_HueWrapsAround()
    {
        var a = Color.FromHsv(0f, 1f, 1f);
        var b = Color.FromHsv(1f, 1f, 1f);
        var c = Color.FromHsv(2f, 1f, 1f);
        Assert.Equal(a, b);
        Assert.Equal(a, c);
    }

    [Fact]
    public void FromHsv_ClampsSaturationAndValue()
    {
        var hi = Color.FromHsv(0f, 5f, 5f);
        var lo = Color.FromHsv(0f, -1f, -1f);
        Assert.Equal(Color.Red, hi);
        Assert.Equal(new Color(0, 0, 0), lo);
    }

    [Fact]
    public void FromHsv_RespectsAlpha()
    {
        Assert.Equal((byte)42, Color.FromHsv(0f, 1f, 1f, 42).A);
    }

    [Fact]
    public void IsClosedTo_ExactMatch()
    {
        Assert.True(new Color(100, 100, 100).IsClosedTo(new Color(100, 100, 100)));
    }

    [Fact]
    public void IsClosedTo_WithinDefaultTolerance()
    {
        // sqrt(2^2 + 2^2 + 2^2) = ~3.46 < default tolerance 8
        Assert.True(new Color(100, 100, 100).IsClosedTo(new Color(102, 102, 102)));
    }

    [Fact]
    public void IsClosedTo_OutsideTolerance()
    {
        Assert.False(new Color(0, 0, 0).IsClosedTo(new Color(50, 0, 0)));
    }

    [Fact]
    public void IsClosedTo_IgnoresAlpha()
    {
        Assert.True(new Color(10, 10, 10, 0).IsClosedTo(new Color(10, 10, 10, 255)));
    }
}

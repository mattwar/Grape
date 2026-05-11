using System.Numerics;
using Blitter.Bits;

namespace Blitter.Tests;

public class ColorHelperTests
{
    private const float Eps = 1e-3f;

    [Fact]
    public void Lerp_EndpointsAndMidpoint()
    {
        Assert.Equal(Color.Black, Color.Lerp(Color.Black, Color.White, 0f));
        Assert.Equal(Color.White, Color.Lerp(Color.Black, Color.White, 1f));
        var mid = Color.Lerp(Color.Black, Color.White, 0.5f);
        Assert.Equal((byte)127, mid.R);
        Assert.Equal((byte)127, mid.G);
        Assert.Equal((byte)127, mid.B);
    }

    [Fact]
    public void Lerp_ClampsTOutsideRange()
    {
        Assert.Equal(Color.Black, Color.Lerp(Color.Black, Color.White, -1f));
        Assert.Equal(Color.White, Color.Lerp(Color.Black, Color.White, 2f));
    }

    [Fact]
    public void Lerp_InterpolatesAlpha()
    {
        var a = new Color(0, 0, 0, 0);
        var b = new Color(255, 255, 255, 255);
        var mid = Color.Lerp(a, b, 0.5f);
        Assert.Equal((byte)127, mid.A);
    }

    [Fact]
    public void WithRedGreenBlue_ReplaceSingleChannel()
    {
        var c = new Color(10, 20, 30, 200);
        Assert.Equal(new Color(99, 20, 30, 200), c.WithRed(99));
        Assert.Equal(new Color(10, 99, 30, 200), c.WithGreen(99));
        Assert.Equal(new Color(10, 20, 99, 200), c.WithBlue(99));
        Assert.Equal(new Color(10, 20, 30, 0), c.WithAlpha(0));
    }

    [Fact]
    public void Darken_ZeroIsIdentity()
    {
        var c = new Color(100, 150, 200, 128);
        Assert.Equal(c, c.Darken(0f));
    }

    [Fact]
    public void Darken_OneIsBlackPreservingAlpha()
    {
        var c = new Color(100, 150, 200, 128);
        var d = c.Darken(1f);
        Assert.Equal((byte)0, d.R);
        Assert.Equal((byte)0, d.G);
        Assert.Equal((byte)0, d.B);
        Assert.Equal((byte)128, d.A);
    }

    [Fact]
    public void Darken_HalfIsHalfRGB()
    {
        var c = new Color(200, 100, 50, 255);
        var d = c.Darken(0.5f);
        Assert.Equal((byte)100, d.R);
        Assert.Equal((byte)50, d.G);
        Assert.Equal((byte)25, d.B);
    }

    [Fact]
    public void Lighten_ZeroIsIdentity()
    {
        var c = new Color(100, 150, 200, 128);
        Assert.Equal(c, c.Lighten(0f));
    }

    [Fact]
    public void Lighten_OneIsWhitePreservingAlpha()
    {
        var c = new Color(100, 150, 200, 128);
        var l = c.Lighten(1f);
        Assert.Equal((byte)255, l.R);
        Assert.Equal((byte)255, l.G);
        Assert.Equal((byte)255, l.B);
        Assert.Equal((byte)128, l.A);
    }

    [Fact]
    public void ToHsv_RoundTripsWithFromHsv_ForPureColors()
    {
        var samples = new[] { Color.Red, Color.Green, Color.Blue, Color.Yellow, Color.Magenta, Color.Cyan };
        foreach (var src in samples)
        {
            var (h, s, v, _) = src.ToHsv();
            var back = Color.FromHsv(h, s, v);
            Assert.Equal(src.R, back.R);
            Assert.Equal(src.G, back.G);
            Assert.Equal(src.B, back.B);
        }
    }

    [Fact]
    public void ToHsv_GrayHasZeroSaturation()
    {
        var (_, s, v, _) = new Color(128, 128, 128).ToHsv();
        Assert.Equal(0f, s, Eps);
        Assert.InRange(v, 0.49f, 0.52f);
    }

    [Fact]
    public void FromVector4_RoundTripsWithImplicit()
    {
        var c = new Color(33, 77, 200, 200);
        Vector4 v = c;                          // implicit
        var back = Color.FromVector4(v);
        Assert.Equal(c, back);
    }

    [Fact]
    public void FromVector4_ClampsOutOfRange()
    {
        var c = Color.FromVector4(new Vector4(2f, -1f, 0.5f, 1f));
        Assert.Equal((byte)255, c.R);
        Assert.Equal((byte)0, c.G);
        Assert.Equal((byte)127, c.B);
        Assert.Equal((byte)255, c.A);
    }

    // -------------------- Parse --------------------

    [Theory]
    [InlineData("#fff", 255, 255, 255, 255)]
    [InlineData("fff", 255, 255, 255, 255)]
    [InlineData("#3af", 0x33, 0xaa, 0xff, 255)]
    [InlineData("#3afc", 0x33, 0xaa, 0xff, 0xcc)]
    [InlineData("#ffffff", 255, 255, 255, 255)]
    [InlineData("#FFFFFF", 255, 255, 255, 255)]
    [InlineData("#ff8000", 0xff, 0x80, 0x00, 255)]
    [InlineData("ff800040", 0xff, 0x80, 0x00, 0x40)]
    [InlineData("rgb(255,0,128)", 255, 0, 128, 255)]
    [InlineData("rgb( 10 , 20 , 30 )", 10, 20, 30, 255)]
    [InlineData("rgba(10,20,30,255)", 10, 20, 30, 255)]
    [InlineData("rgba(10,20,30,0.5)", 10, 20, 30, 127)]
    public void Parse_AcceptsCommonFormats(string input, int r, int g, int b, int a)
    {
        var c = Color.Parse(input);
        Assert.Equal((byte)r, c.R);
        Assert.Equal((byte)g, c.G);
        Assert.Equal((byte)b, c.B);
        Assert.Equal((byte)a, c.A);
    }

    [Theory]
    [InlineData("")]
    [InlineData("xyz")]
    [InlineData("#12")]
    [InlineData("#1234567")]    // 7 hex digits
    [InlineData("rgb(1,2)")]
    [InlineData("rgb(1,2,300)")] // out of range
    [InlineData("rgba(1,2,3,2.0)")]
    public void TryParse_RejectsInvalid(string input)
    {
        Assert.False(Color.TryParse(input, out _));
    }

    [Fact]
    public void Parse_ThrowsOnInvalid() =>
        Assert.Throws<FormatException>(() => Color.Parse("not-a-color"));
}

public class GradientTests
{
    [Fact]
    public void FromColors_EndpointsExact()
    {
        var g = Gradient.FromColors(Color.Black, Color.Red, Color.White);
        Assert.Equal(Color.Black, g.Sample(0f));
        Assert.Equal(Color.White, g.Sample(1f));
        Assert.Equal(Color.Red, g.Sample(0.5f));
    }

    [Fact]
    public void Sample_ClampsOutsideRange()
    {
        var g = Gradient.FromColors(Color.Black, Color.White);
        Assert.Equal(Color.Black, g.Sample(-1f));
        Assert.Equal(Color.White, g.Sample(2f));
    }

    [Fact]
    public void Sample_InterpolatesBetweenStops()
    {
        var g = Gradient.FromColors(Color.Black, Color.White);
        var mid = g.Sample(0.5f);
        Assert.Equal((byte)127, mid.R);
        Assert.Equal((byte)127, mid.G);
        Assert.Equal((byte)127, mid.B);
    }

    [Fact]
    public void Stops_AreSortedRegardlessOfInputOrder()
    {
        var g = new Gradient(new[]
        {
            (1f, Color.White),
            (0f, Color.Black),
            (0.5f, Color.Red),
        });
        Assert.Equal(Color.Black, g.Sample(0f));
        Assert.Equal(Color.Red, g.Sample(0.5f));
        Assert.Equal(Color.White, g.Sample(1f));
    }

    [Fact]
    public void Constructor_RejectsTooFewStops()
    {
        Assert.Throws<ArgumentException>(() =>
            new Gradient(new[] { (0f, Color.Black) }));
        Assert.Throws<ArgumentException>(() => Gradient.FromColors(Color.Black));
    }

    [Fact]
    public void StopCount_ReflectsInput()
    {
        var g = Gradient.FromColors(Color.Black, Color.Red, Color.Yellow, Color.White);
        Assert.Equal(4, g.StopCount);
    }
}

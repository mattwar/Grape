using System.Numerics;

namespace Blitter.Tests;

// Covers Image.Render3D's two readback paths:
//  - PixelFormat.ABGR8888  -> memcpy fast path
//  - other formats         -> per-pixel conversion fallback
//
// Tagged Gpu so machines without a usable graphics driver can filter
// it out:  dotnet test --filter "Category!=Gpu"
[Trait("Category", "Gpu")]
public class ImageRender3DTests
{
    [Theory]
    [InlineData(PixelFormat.ABGR8888)]
    [InlineData(PixelFormat.RGBA8888)]
    public void Render3D_FillsBackground_AcrossPixelFormats(PixelFormat format)
    {
        using var image = Bitmap.Create(16, 16, format);
        var background = new Color(40, 80, 160);

        image.Render3D(background, _ => { });

        // Center pixel should match the background regardless of the
        // surface format; the conversion fallback must round-trip the
        // channels correctly.
        Assert.Equal(background, image.GetPixel(8, 8));
    }

    [Theory]
    [InlineData(PixelFormat.ABGR8888)]  // direct-copy path
    [InlineData(PixelFormat.RGBA8888)]  // per-pixel-conversion path
    public void Render3D_TranslucentBackground_BlendsOverExistingPixels(PixelFormat format)
    {
        using var image = Bitmap.Create(4, 4, format);

        // Fill with opaque red wallpaper.
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                image.SetPixel(x, y, new Color(255, 0, 0, 255));

        // SrcOver translucent green (alpha 128) over red:
        //   out_r = (0 * 128 + 255 * 127 + 127) / 255 = 127
        //   out_g = (255 * 128 + 0 * 127 + 127) / 255 = 128
        //   out_b = 0
        //   out_a = 128 + (255 * 127 + 127) / 255 = 255
        image.Render3D(new Color(0, 255, 0, 128), _ => { });

        var px = image.GetPixel(2, 2);
        Assert.Equal(127, px.R);
        Assert.Equal(128, px.G);
        Assert.Equal(0, px.B);
        Assert.Equal(255, px.A);
    }
}

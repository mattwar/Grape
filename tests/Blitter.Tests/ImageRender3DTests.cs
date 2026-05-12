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
        using var image = Image.Create(16, 16, format);
        var background = new Color(40, 80, 160);

        image.Render3D(background, _ => { });

        // Center pixel should match the background regardless of the
        // surface format; the conversion fallback must round-trip the
        // channels correctly.
        Assert.Equal(background, image.GetPixel(8, 8));
    }
}

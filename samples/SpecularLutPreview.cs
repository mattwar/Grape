#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/SpecularLutPreview.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// Visual sanity check for `Textures.SpecularLut` -- 
// the 256x256 precomputed split-sum BRDF integration texture used by PBR
// specular image-based lighting. Should match the canonical PBR
// LUT image: bright red curling from the bottom-left, green pooled
// in the lower-left, smooth gradients everywhere.
//
// Axes (Karis 2013 split-sum convention):
//   U (left -> right)  = NdotV     (grazing -> head-on)
//   V (top  -> bottom) = roughness (mirror  -> rough)
//
// Channels:
//   R = Fresnel scale, G = Fresnel bias.
//   The LUT only writes R and G; B is always 0.

using Blitter;
using Blitter.Bits;

const int LutSize = 256;
const int Inset = 60; // room for axis labels

var window = new Window2D
{
    Title = "Specular BRDF LUT preview",
    BackgroundColor = new Color(18, 20, 28),
    CloseKey = Key.Escape,
};
window.Renderer.SetLogicalSize(LutSize * 2 + Inset * 2, LutSize * 2 + Inset * 2,
    LogicalPresentation.Letterbox);

// Cache the property reference -- the first access bakes the
// 256x256 image (a few hundred ms of CPU work); we don't want to
// re-resolve the lazy field every frame.
var lut = Textures.SpecularLut;

await window.RunAsync(rd =>
{
    // Source LUT is 256x256; draw at 2x so the gradients are
    // easier to read on a typical monitor.
    var dest = new Rect(Inset, Inset, LutSize * 2, LutSize * 2);

    // Light backing frame so the dark green/black corner of the
    // LUT is still visible.
    rd.DrawColor = new Color(40, 44, 56);
    rd.DrawFillRect(new Rect(dest.X - 2, dest.Y - 2, dest.Width + 4, dest.Height + 4));

    rd.DrawImage(lut, dest);

    rd.DrawColor = new Color(220, 225, 235);
    rd.DrawDebugText((int)dest.X, (int)dest.Y - 28,
        "Textures.SpecularLut  (R = Fresnel scale, G = bias)", scale: 1.5f);

    // U axis (NdotV)
    rd.DrawDebugText((int)dest.X, (int)(dest.Y + dest.Height) + 8, "NdotV: 0 (grazing)", scale: 1.2f);
    rd.DrawDebugText((int)(dest.X + dest.Width) - 140, (int)(dest.Y + dest.Height) + 8,
        "1 (head-on)", scale: 1.2f);

    // V axis (roughness) -- image Y grows downward, and the bake
    // writes roughness=0 at y=0, so the top edge is smooth and the
    // bottom edge is rough.
    rd.DrawDebugText((int)dest.X - Inset + 8, (int)dest.Y, "rough: 0", scale: 1.2f);
    rd.DrawDebugText((int)dest.X - Inset + 8, (int)(dest.Y + dest.Height) - 16, "rough: 1", scale: 1.2f);
});

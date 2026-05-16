#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/DebugText.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

using System.Numerics;
using Blitter;
using Blitter.Bits;

// Demonstrates drawing 3D debug text: render.DrawDebugText(string, Matrix4x4).

var window = new Window3D
{
    Title = "Debug Text",
    BackgroundColor = new Color(16, 0, 32),
    FullScreen = true,
    CloseKey = Key.Escape,
};

long frameCount = 0;

await window.RunAsync(rd =>
{
    frameCount++;
    var t = rd.ElapsedSecondsSinceStart;
    var aspect = 1f / rd.AspectRatio; // height / width

    // Top line: a fixed banner that swings left-to-right with a gentle rotational wobble and slight vertical bob.
    {
        const string banner = "Hello, 3D world!";
        float swing = 0.4f * MathF.Sin(t * 0.8f);
        float bob   = 0.04f * MathF.Sin(t * 2.3f);
        float roll  = 0.15f * MathF.Sin(t * 1.7f);
        float scale = 0.08f;
        var transform = Matrix4x4.CreateTranslation(-banner.Length / 2f, -0.5f, 0f)
            .Scale(scale)
            .RotateZ(roll)
            .Translate(swing, 0.4f + bob, 0f)
            .Scale(aspect, 1f, 1f);
        rd.DrawDebugText(banner, transform);
    }

    // Bottom line: live readout that grows/shrinks character count.
    {
        var live = $"t={t:F2}s frame={frameCount}";
        float scale = 0.06f;
        float widthInNdc = live.Length * scale;
        var transform = Matrix4x4.CreateScale(scale)
            .Translate(-widthInNdc / 2f, -0.5f, 0f)
            .Scale(aspect, 1f, 1f);
        rd.DrawDebugText(live, transform);
    }
});
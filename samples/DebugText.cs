#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/DebugText.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     dotnet build src/Grape.Graphics/Grape.Graphics.csproj
//
// The samples/NuGet.config in this folder pulls Grape.Graphics from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.

using System.Numerics;
using Grape;

// Demonstrates 3D debug text. The string is updated every frame to show
// elapsed time and frame number; the text mesh re-uploads via the renderer's
// array-keyed mesh cache, sharing one GPU vertex buffer and one GPU font
// atlas texture across all draws.

var window = new Window3D
{
    Title = "Debug Text",
    BackgroundColor = new Color(16, 0, 32),
    FullScreen = true,
    CloseKey = Key.Escape,
};

long frameCount = 0;

window.Rendering += (w, frame) =>
{
    frameCount++;
    var t = (float)frame.ElapsedSinceWindowCreated.TotalSeconds;
    var (width, height) = w.Size;
    var aspect = (float)height / width;

    // Top line: a fixed banner that swings left-to-right with a gentle
    // rotational wobble and slight vertical bob.
    {
        const string banner = "Hello, 3D world!";
        float swing = 0.4f * MathF.Sin(t * 0.8f);
        float bob   = 0.04f * MathF.Sin(t * 2.3f);
        float roll  = 0.15f * MathF.Sin(t * 1.7f);
        float scale = 0.08f;
        var transform =
            Matrix4x4.CreateTranslation(-banner.Length / 2f, -0.5f, 0f) *
            Matrix4x4.CreateScale(scale) *
            Matrix4x4.CreateRotationZ(roll) *
            Matrix4x4.CreateTranslation(swing, 0.4f + bob, 0f) *
            Matrix4x4.CreateScale(aspect, 1f, 1f);
        frame.Renderer.RenderDebugText(banner, transform);
    }

    // Bottom line: live readout that grows/shrinks character count.
    {
        var live = $"t={t:F2}s frame={frameCount}";
        float scale = 0.06f;
        float widthInNdc = live.Length * scale;
        var transform =
            Matrix4x4.CreateScale(scale) *
            Matrix4x4.CreateTranslation(-widthInNdc / 2f, -0.5f, 0f) *
            Matrix4x4.CreateScale(aspect, 1f, 1f);
        frame.Renderer.RenderDebugText(live, transform);
    }

    w.Invalidate(); // schedule the next frame
};

await window.WaitForCloseAsync();

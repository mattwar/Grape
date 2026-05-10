#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/RicochetRocket.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj
//
// The samples/NuGet.config in this folder pulls Blitter from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.
//
// Asset paths below resolve relative to this source file, so the
// sample works regardless of the shell's current directory.

using System.Runtime.CompilerServices;
using Blitter;
using Blitter.Blocks;

static string SampleAsset(string name, [CallerFilePath] string sourcePath = "")
    => Path.Combine(Path.GetDirectoryName(sourcePath)!, name);

// Fixed design surface. The renderer letterboxes this into whatever
// the actual window size is, so the playfield stays a constant
// 1920x1080 regardless of monitor resolution or fullscreen toggles.
const int DesignW = 1920;
const int DesignH = 1080;

var window = new Window2D(DesignW, DesignH)
{
    Title = "Ricochet Rocket",
    BackgroundColor = new Color(0, 20, 0, 0),
    FullScreen = true,
    CloseKey = Key.Escape,
};

window.Renderer.SetLogicalSize(DesignW, DesignH, LogicalPresentation.Letterbox);

var rocketImage = Image.Load(SampleAsset("rocket.png"));
rocketImage.SetAlpha(0, rocketImage.GetPixel(0, 0)); // make the background transparent
var rocket = new Sprite2D(rocketImage, DesignW / 2, DesignH / 2, 0.1f)
{
    Speed = 600f,
    Heading = 45f
};

var sound = Sound.LoadWAV(SampleAsset("szwoopy.wav"));

window.KeyDown += (_, e) =>
{
    switch (e.Key)
    {
        case Key.Left:
            rocket.Heading = (rocket.Heading + 350f) % 360f;
            break;
        case Key.Right:
            rocket.Heading = (rocket.Heading + 10f) % 360f;
            break;
        case Key.Up:
            rocket.Speed = Math.Min(rocket.Speed + 50f, 1000f);
            break;
        case Key.Down:
            rocket.Speed = Math.Max(rocket.Speed - 50f, 0f);
            break;
    }
};

window.Rendering += (w, rd) =>
{
    if (rocket.Update(rd.GetUpdateContext()))
    {
        var bounce = false;

        // bounce off left/right walls
        if (rocket.CenterX < 0)
        {
            rocket.ChangeVelocity((vx, vy) => (Math.Abs(vx), vy));
            bounce = true;
        }
        else if (rocket.CenterX > DesignW)
        {
            rocket.ChangeVelocity((vx, vy) => (-Math.Abs(vx), vy));
            bounce = true;
        }

        // bounce off top/bottom walls
        if (rocket.CenterY < 0)
        {
            rocket.ChangeVelocity((vx, vy) => (vx, Math.Abs(vy)));
            bounce = true;
        }
        else if (rocket.CenterY > DesignH)
        {
            rocket.ChangeVelocity((vx, vy) => (vx, -Math.Abs(vy)));
            bounce = true;
        }

        // make the rocket point in the direction it's moving
        rocket.Rotation = rocket.Heading;

        if (bounce)
        {
            rocket.Heading = (rocket.Heading + Random.Shared.Next(-10, 10) + 360f) % 360f;
            Audio.Play(sound, volume: .2f);
        }
    }

    rocket.Draw(rd);

    rd.DrawColor = new Color(255, 255, 255);
    rd.DrawDebugText(
        0, 10,
        $"heading: {rocket.Heading:#} speed: {rocket.Speed:#} rotation: {rocket.Rotation:#} x: {rocket.CenterX:#} y: {rocket.CenterY:#} dt: {rd.ElapsedSinceLastRender.TotalMilliseconds:0.000}ms",
        scale: 2f);

    w.Invalidate(); // request the next rendering
};

await window.WaitForCloseAsync();

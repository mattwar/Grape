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

using Blitter;
using Blitter.Bits;
using Blitter.Blocks;

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

var rocketImage = Bitmap.Load(Asset.GetPathRelativeToCaller("rocket.png"));
rocketImage.SetAlpha(0, rocketImage.GetPixel(0, 0)); // make the background transparent
var rocket = new Sprite2D(rocketImage, DesignW / 2, DesignH / 2, 0.1f)
{
    Speed = 600f,
    Heading = 45f
};

var sound = Sound.LoadWAV(Asset.GetPathRelativeToCaller("szwoopy.wav"));

await window.RunAsync(rd =>
{
    // Per-frame input: edges for arrow-key tap response. Holding an
    // arrow key only fires once per press, matching the original
    // KeyDown event behavior.
    var input = window.Input;
    if (input.WasJustPressed(Key.Left))
        rocket.Heading = (rocket.Heading + 350f) % 360f;
    if (input.WasJustPressed(Key.Right))
        rocket.Heading = (rocket.Heading + 10f) % 360f;
    if (input.WasJustPressed(Key.Up))
        rocket.Speed = Math.Clamp(rocket.Speed + 50f, 0f, 1000f);
    if (input.WasJustPressed(Key.Down))
        rocket.Speed = Math.Clamp(rocket.Speed - 50f, 0f, 1000f);

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

    rd.DrawColor = Color.White;
    rd.DrawDebugText(
        0, 10,
        $"heading: {rocket.Heading:#} speed: {rocket.Speed:#} rotation: {rocket.Rotation:#} x: {rocket.CenterX:#} y: {rocket.CenterY:#} dt: {rd.ElapsedSinceLastRender.TotalMilliseconds:0.000}ms",
        scale: 2f);
});
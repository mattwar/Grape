#:package Grape.Graphics@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/RicochetRocket.cs
//
// While Grape.Graphics is unpublished, build a local copy first:
//
//     ./pack-local.ps1
//
// The samples/NuGet.config in this folder pulls Grape.Graphics from
// ./artifacts/nuget when present, falling back to nuget.org otherwise.
//
// Asset paths below are resolved relative to the current working directory,
// so run this file from the repository root.

using Grape;
using Grape.Jelly;

var window = new Window2D(800, 600)
{
    Title = "Ricochet Rocket",
    BackgroundColor = new Color(0, 20, 0, 0),
    FullScreen = true
};

var icon = Image.LoadImage("samples/grape.bmp");
icon.SetAlpha(0, icon.GetPixel(0, 0));
window.Icon = icon;

var rocketImage = Image.LoadImage("samples/rocket.png");
rocketImage.SetAlpha(0, rocketImage.GetPixel(0, 0)); // make the background transparent
var rocket = new Sprite(rocketImage, window.Size.Width / 2, window.Size.Height / 2, 0.2f)
{
    Speed = 600f,
    Heading = 45f,
};

var sound = AudioData.LoadWAV("samples/szwoopy.wav");

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
        case Key.Escape:
            window.Dispose();
            break;
    }
};

window.RenderingFrame += (w, frame) =>
{
    var updateContext = new UpdateContext
    {
        ElapsedSinceStart = frame.ElapsedSinceWindowCreated,
        ElaspsedSinceLastUpdate = frame.ElapsedSinceLastFrame,
        Bounds = new Rect(0, 0, w.Size.Width, w.Size.Height)
    };

    if (rocket.Update(updateContext))
    {
        var bounce = false;

        // bounce off left/right walls
        if (rocket.CenterX < 0)
        {
            rocket.ChangeVelocity((vx, vy) => (Math.Abs(vx), vy));
            bounce = true;
        }
        else if (rocket.CenterX > w.Size.Width)
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
        else if (rocket.CenterY > w.Size.Height)
        {
            rocket.ChangeVelocity((vx, vy) => (vx, -Math.Abs(vy)));
            bounce = true;
        }

        // make the rocket point in the direction it's moving
        rocket.Rotation = rocket.Heading;

        if (bounce)
        {
            rocket.Heading = (rocket.Heading + Random.Shared.Next(-10, 10) + 360f) % 360f;
            _ = Audio.Play(sound, volume: .2f);
        }
    }

    rocket.Render(frame.Renderer);

    frame.Renderer.DrawColor = new Color(255, 255, 255);
    frame.Renderer.RenderDebugText(
        0, 10,
        $"heading: {rocket.Heading:#} speed: {rocket.Speed:#} rotation: {rocket.Rotation:#} x: {rocket.CenterX:#} y: {rocket.CenterY:#}",
        scale: 4f);

    w.Invalidate(); // schedule the next frame
};

await window.WaitForDisposeAsync();

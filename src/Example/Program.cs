using SDL3;
using SDL3.Model;
using Grape;

var window = new Window(800, 600)
{
    Title = "Grape",
    BackgroundColor = new SDL.Color { R = 0, G = 20, B = 0, A = 0 },
    FullScreen = true
};

var icon = Surface.LoadImage("grape.bmp");
icon.SetAlpha(0, icon.GetPixel(0, 0));
window.Icon = icon;

var rocketImage = Surface.LoadImage("rocket.png");
rocketImage.SetAlpha(0, rocketImage.GetPixel(0, 0)); // make the background transparent
var rocket = new Sprite(rocketImage, window.Size.Width / 2, window.Size.Height / 2, 0.2f);
//rocket.SpinVelocity = 90; // how many degrees spin per second

window.KeyDown += (window, e) =>
{
    switch (e.Key)
    {
        case SDL.Keycode.Left:
            rocket.VelocityX += -10f;
            break;
        case SDL.Keycode.Right:
            rocket.VelocityX += 10f;
            break;
        case SDL.Keycode.Up:
            rocket.VelocityY += -10f;
            break;
        case SDL.Keycode.Down:
            rocket.VelocityY += 10f;
            break;
    }
};

window.Rendering += (window, renderer) =>
{
    rocket.Render(renderer);

#if DEBUG
    // render debug text on screen
    renderer.DrawColor = new SDL.Color { R = 255, G = 255, B = 255, A = 255 };
    renderer.RenderDebugText(0, 10, $"vx: {rocket.VelocityX} vy: {rocket.VelocityY} vr: {rocket.SpinVelocity} x: {rocket.CenterX:#} y: {rocket.CenterY:#} r: {rocket.Rotation:#}", scale: 4f);
#endif
};

// game loop
var startTime = DateTime.UtcNow;
var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(4));
while (!window.IsDisposed)
{
    await timer.WaitForNextTickAsync();

    var updateContext = new UpdateContext 
    { 
        Time = DateTime.UtcNow - startTime,
        Bounds = new SDL.Rect { X = 0, Y = 0, W = window.Size.Width, H = window.Size.Height }
    };

    if (rocket.Update(updateContext))
    {
        if (rocket.CenterX < 0 || rocket.CenterX > window.Size.Width)
            rocket.VelocityX = -rocket.VelocityX; // bounce off left/right walls
        if (rocket.CenterY < 0 || rocket.CenterY > window.Size.Height)
            rocket.VelocityY = -rocket.VelocityY; // bounce off top/bottom walls
        window.Invalidate(); // request a redraw if something changed
    }
}

Console.WriteLine("Done");
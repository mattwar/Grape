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
rocket.Speed = 600f;
rocket.Heading = 45f;

var sound = AudioData.LoadWAV("szwoopy.wav");

window.KeyDown += Window_KeyDown;
window.Rendering += Window_Rendering;

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
        var bounce = false;
        // bounce off left/right walls
        if (rocket.CenterX < 0)
        {
            rocket.ChangeVelocity((vx, vy) => (Math.Abs(vx), vy));
            bounce = true;
        }
        else if (rocket.CenterX > window.Size.Width)
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
        else if (rocket.CenterY > window.Size.Height)
        {
            rocket.ChangeVelocity((vx, vy) => (vx, -Math.Abs(vy)));
            bounce = true;
        }

        // make the rocket point in the direction it's moving
        rocket.Rotation = rocket.Heading;

        window.Invalidate();

        if (bounce)
        {
            rocket.Heading = (rocket.Heading + Random.Shared.Next(-10, 10) + 360f) % 360f; // add a little randomness to the bounce
        }

        if (bounce)
        {
            _ = Audio.Play(sound, volume:.2f);
        }
    }
}

Console.WriteLine("Done");


void Window_KeyDown(Window window, SDL.KeyboardEvent context)
{
    switch (context.Key)
    {
        case SDL.Keycode.Left:
            rocket.Heading = (rocket.Heading + 350f) % 360f; // rotate left 10%
            break;
        case SDL.Keycode.Right:
            rocket.Heading = (rocket.Heading + 10f) % 360f; // rotate right 10%
            break;
        case SDL.Keycode.Up:
            rocket.Speed = Math.Min(rocket.Speed + 50f, 1000f); // increase speed by 10, maximum 100
            break;
        case SDL.Keycode.Down:
            rocket.Speed = Math.Max(rocket.Speed - 50f, 0f); // decrease speed by 10, minimum 0
            break;
        case SDL.Keycode.Escape:
            Application.Current.Dispose();
            break;
    }
}

void Window_Rendering(Window window, Renderer renderer)
{
    rocket.Render(renderer);

#if DEBUG
    // render debug text on screen
    renderer.DrawColor = new SDL.Color { R = 255, G = 255, B = 255, A = 255 };
    renderer.RenderDebugText(0, 10, $"heading: {rocket.Heading:#} speed: {rocket.Speed:#} heading: {rocket.Rotation:#} x: {rocket.CenterX:#} y: {rocket.CenterY:#}", scale: 4f);
#endif
}
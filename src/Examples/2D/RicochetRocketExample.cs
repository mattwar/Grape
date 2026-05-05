using System.Security.Principal;
using Grape;
using Grape.Jelly;

internal static class RicochetRocketExample
{
    public static async Task Run()
    {
        var window = new Window2D(800, 600)
        {
            Title = "Spinning Rocket",
            BackgroundColor = new Color(0, 20, 0, 0),
            FullScreen = true
        };

        var icon = Image.LoadImage("grape.bmp");
        icon.SetAlpha(0, icon.GetPixel(0, 0));
        window.Icon = icon;

        var rocketImage = Image.LoadImage("rocket.png");
        rocketImage.SetAlpha(0, rocketImage.GetPixel(0, 0)); // make the background transparent
        var rocket = new Sprite(rocketImage, window.Size.Width / 2, window.Size.Height / 2, 0.2f);
        rocket.Speed = 600f;
        rocket.Heading = 45f;

        var sound = AudioData.LoadWAV("szwoopy.wav");

        window.KeyDown += (window, args) =>
        {
            switch (args.Key)
            {
                case Key.Left:
                    rocket.Heading = (rocket.Heading + 350f) % 360f; // rotate left 10%
                    break;
                case Key.Right:
                    rocket.Heading = (rocket.Heading + 10f) % 360f; // rotate right 10%
                    break;
                case Key.Up:
                    rocket.Speed = Math.Min(rocket.Speed + 50f, 1000f); // increase speed by 10, maximum 100
                    break;
                case Key.Down:
                    rocket.Speed = Math.Max(rocket.Speed - 50f, 0f); // decrease speed by 10, minimum 0
                    break;
                case Key.Escape:
                    Application.Current.Dispose();
                    break;
            }
        };

        window.RenderingFrame += (window, args) =>
        {
            var updateContext = new UpdateContext
            {
                ElapsedSinceStart = args.ElapsedSinceWindowCreated,
                ElaspsedSinceLastUpdate = args.ElapsedSinceLastFrame,
                Bounds = new Rect(0, 0, window.Size.Width, window.Size.Height)
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

                if (bounce)
                {
                    rocket.Heading = (rocket.Heading + Random.Shared.Next(-10, 10) + 360f) % 360f; // add a little randomness to the bounce
                    _ = Audio.Play(sound, volume: .2f);
                }
            }

            rocket.Render(args.Renderer);
#if DEBUG
            // render debug text on screen
            args.Renderer.DrawColor = new Color(255, 255, 255);
            args.Renderer.RenderDebugText(0, 10, $"heading: {rocket.Heading:#} speed: {rocket.Speed:#} heading: {rocket.Rotation:#} x: {rocket.CenterX:#} y: {rocket.CenterY:#}", scale: 4f);
#endif

            window.Invalidate(); // trigger next frame
        };

        await window.WaitForDisposeAsync();

        Console.WriteLine("Done");
    }
}

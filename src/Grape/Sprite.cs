using SDL3;
using SDL3.Model;

namespace Grape;

/// <summary>
/// A prop that moves itself.
/// </summary>
public class Sprite : Prop
{
    /// <summary>
    /// The image to render.
    /// </summary>
    public Surface? Image { get; set; }

    /// <summary>
    /// The X position of the center of the sprite.
    /// </summary>    
    public float CenterX { get; set; }

    /// <summary>
    /// The Y position of the center of the sprite.
    /// </summary>
    public float CenterY { get; set; }

    /// <summary>
    /// The direction and how far the sprite moves along the Y axis in a second.
    /// </summary>
    public float VelocityX { get; set; }

    /// <summary>
    /// The direction and how far the sprite moves along the X axis in a second.
    /// </summary>
    public float VelocityY { get; set; }

    /// <summary>
    /// The current rotation in degrees.
    /// </summary>
    public float Rotation { get; set; }

    /// <summary>
    /// The direction and how far the sprite rotates in a second.
    /// </summary>
    public float SpinVelocity { get; set; }

    /// <summary>
    /// The scale factor to apply to the image.
    /// </summary>
    public float Scale { get; set; } = 1f;

    /// <summary>
    /// The flip mode to apply when rendering the image.
    /// </summary>
    public SDL.FlipMode Flipped = SDL.FlipMode.None;

    private TimeSpan _lastUpdate = TimeSpan.Zero;

    public Sprite()
    {
    }

    public Sprite(Surface image, float centerX, float centerY, float scale = 1f)
    {
        this.Image = image;
        this.CenterX = centerX;
        this.CenterY = centerY;
        this.Scale = scale;
    }

    public override bool Update(in UpdateContext context)
    {
        double sec = context.Time.TotalSeconds;
        var timeDelta = context.Time - _lastUpdate;

        if (timeDelta.TotalMilliseconds < 10)
            return false;

        float newRotation = this.Rotation;
        if (this.SpinVelocity != 0f)
        {
            var rotationDelta = (float)(this.SpinVelocity * timeDelta.TotalSeconds);
            newRotation = (this.Rotation + rotationDelta) % 360f;
        }

        float newCenterX = this.CenterX;
        if (this.VelocityX != 0f)
        {
            var deltaX = (float)(this.VelocityX * timeDelta.TotalSeconds);
            newCenterX = this.CenterX + deltaX;
        }

        float newCenterY = this.CenterY;
        if (this.VelocityY != 0f)
        {
            var deltaY = (float)(this.VelocityY * timeDelta.TotalSeconds);
            newCenterY = this.CenterY + deltaY;
        }

        var changed = newRotation != this.Rotation
            || newCenterX != this.CenterX
            || newCenterY != this.CenterY
            || _lastUpdate == TimeSpan.Zero; // always update the first time

        if (changed)
        {
            this.Rotation = newRotation;
            this.CenterX = newCenterX;
            this.CenterY = newCenterY;
            _lastUpdate = context.Time;
            return true;
        }

        return false;
    }

    public override void Render(Renderer renderer)
    {
        if (this.Image is { } image)
        {
            var size = image.Size;
            var scaledWidth = size.Width * this.Scale;
            var scaledHeight = size.Height * this.Scale;
            var x = this.CenterX - scaledWidth / 2;
            var y = this.CenterY - scaledHeight / 2;
            var source = new SDL.FRect { X = 0, Y = 0, W = size.Width, H = size.Height };
            var dest = new SDL.FRect { X = x, Y = y, W = scaledWidth, H = scaledHeight };
            var center = new SDL.FPoint { X = scaledWidth / 2f, Y = scaledHeight / 2f };

            if (this.Rotation != 0f || this.Flipped != SDL.FlipMode.None)
            {
                renderer.RenderSurfaceRotated(image, source, dest, this.Rotation, center, this.Flipped);
            }
            else
            {
                renderer.RenderSurface(image, source, dest);
            }
        }
    }
}

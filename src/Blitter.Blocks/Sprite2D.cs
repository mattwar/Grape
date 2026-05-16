using System.Numerics;

namespace Blitter.Blocks;

/// <summary>
/// A prop that moves itself.
/// </summary>
public class Sprite2D : Prop2D
{
    /// <summary>
    /// The image to render.
    /// </summary>
    public Texture2D? Image { get; set; }

    /// <summary>
    /// The X position of the center of the sprite.
    /// </summary>    
    public float CenterX { get; set; }

    /// <summary>
    /// The Y position of the center of the sprite.
    /// </summary>
    public float CenterY { get; set; }

    /// <summary>
    /// The direction of movement.
    /// </summary>
    public float Heading { get; set; }

    /// <summary>
    /// The speed of the sprite in pixels/units per second along the heading.
    /// </summary>
    public float Speed { get; set; }

    /// <summary>
    /// The current orientation in degrees.
    /// </summary>
    public float Rotation { get; set; }

    /// <summary>
    /// How many degrees the sprite rotates in a second.
    /// </summary>
    public float RotationSpeed { get; set; }

    /// <summary>
    /// The scale factor to apply to the image.
    /// </summary>
    public float Scale { get; set; } = 1f;

    /// <summary>
    /// The flip mode to apply when rendering the image.
    /// </summary>
    public FlipMode Flipped = FlipMode.None;

    /// <summary>
    /// Minimum time that must accumulate between successful
    /// <see cref="Update"/> calls. Updates with smaller deltas
    /// are buffered and applied later. Prevents motion from
    /// being lost to per-frame float rounding when the host
    /// loop runs at very high frame rates.
    /// </summary>
    public TimeSpan MinUpdateInterval { get; set; } = TimeSpan.FromMilliseconds(1);

    // Accumulated time from Update calls that did not meet
    // MinUpdateInterval; carried forward to the next Update.
    private TimeSpan _pendingDelta;

    public Sprite2D()
    {
    }

    public Sprite2D(Texture2D image, float centerX, float centerY, float scale = 1f)
    {
        this.Image = image;
        this.CenterX = centerX;
        this.CenterY = centerY;
        this.Scale = scale;
    }

    public override bool Update(in UpdateContext2D context)
    {
        // First call: nothing has happened yet, so just confirm initial state.
        if (context.ElapsedSinceLastUpdate == TimeSpan.Zero)
            return true;

        // Buffer small deltas so motion isn't lost to float rounding when
        // the host renders far faster than physics needs.
        _pendingDelta += context.ElapsedSinceLastUpdate;
        if (_pendingDelta < MinUpdateInterval)
            return false;

        var timeDelta = _pendingDelta;
        _pendingDelta = TimeSpan.Zero;

        float newRotation = this.Rotation;
        if (this.RotationSpeed != 0f)
        {
            var rotationDelta = (float)(this.RotationSpeed * timeDelta.TotalSeconds);
            newRotation = (this.Rotation + rotationDelta) % 360f;
        }

        var speed = this.Speed;
        float newCenterX = this.CenterX;
        float newCenterY = this.CenterY;
        if (speed != 0f)
        {
            (var velocityX, var velocityY) = GetVelocity(speed, Heading);

            var deltaX = (float)(velocityX * timeDelta.TotalSeconds);
            newCenterX = this.CenterX + deltaX;

            var deltaY = (float)(velocityY * timeDelta.TotalSeconds);
            newCenterY = this.CenterY + deltaY;
        }

        var changed = newRotation != this.Rotation
            || newCenterX != this.CenterX
            || newCenterY != this.CenterY;

        if (changed)
        {
            this.Rotation = newRotation;
            this.CenterX = newCenterX;
            this.CenterY = newCenterY;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get velocity components from speed and heading (degrees).
    /// </summary>
    public static (float velocityX, float velocityY) GetVelocity(float speed, float heading)
    {
        double headingRads = (heading - 90f) * (Math.PI / 180.0);
        var velocityX = speed * (float)Math.Cos(headingRads);
        if (MathF.Abs(velocityX) < 0.0001f)
            velocityX = 0f;
        var velocityY = speed * (float)Math.Sin(headingRads);
        if (MathF.Abs(velocityY) < 0.0001f)
            velocityY = 0f;
        return (velocityX, velocityY);
    }

    /// <summary>
    /// Gets speed and heading (degrees) from velocity components.
    /// </summary>
    public static (float speed, float heading) GetSpeedAndHeading(float velocityX, float velocityY)
    {
        var speed = (float)Math.Sqrt(velocityX * velocityX + velocityY * velocityY);
        var heading = (float)(Math.Atan2(velocityY, velocityX) * (180.0 / Math.PI) + 90f);
        if (heading < 0)
            heading += 360f;
        else if (heading >= 360f)
            heading -= 360f;
        return (speed, heading);
    }

    public void ChangeVelocity(Func<float, float, (float vx, float vy)> fn)
    {
        var (vx, vy) = GetVelocity(this.Speed, this.Heading);
        (vx, vy) = fn(vx, vy);
        (this.Speed, this.Heading) = GetSpeedAndHeading(vx, vy);
    }

    public override void Draw(Renderer2D renderer)
    {
        if (this.Image is { } image)
        {
            var size = image.Size;
            var scaledWidth = size.Width * this.Scale;
            var scaledHeight = size.Height * this.Scale;
            var x = this.CenterX - scaledWidth / 2;
            var y = this.CenterY - scaledHeight / 2;
            var source = new Rect(0, 0, size.Width, size.Height);
            var dest = new Rect(x, y, scaledWidth, scaledHeight);
            var center = new Vector2(scaledWidth / 2f, scaledHeight / 2f);

            if (this.Rotation != 0f || this.Flipped != FlipMode.None)
            {
                renderer.DrawImageRotated(image, source, dest, this.Rotation, center, this.Flipped);
            }
            else
            {
                renderer.DrawImage(image, source, dest);
            }
        }
    }
}

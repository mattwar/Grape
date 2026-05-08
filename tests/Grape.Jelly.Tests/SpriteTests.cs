namespace Grape.Jelly.Tests;

public class SpriteTests
{
    private static UpdateContext2D Context(double seconds) =>
        new() { ElapsedSinceLastUpdate = TimeSpan.FromSeconds(seconds) };

    [Fact]
    public void Update_WithNoMotion_AfterFirstFrame_ReturnsFalse()
    {
        var sprite = new Sprite2D { CenterX = 100, CenterY = 100 };

        // First frame (zero elapsed) is treated as "changed" so the sprite renders initially.
        Assert.True(sprite.Update(Context(0)));

        // Subsequent frame with no movement and no rotation: nothing changed.
        Assert.False(sprite.Update(Context(1.0 / 60.0)));
        Assert.Equal(100f, sprite.CenterX);
        Assert.Equal(100f, sprite.CenterY);
    }

    [Fact]
    public void Update_HeadingZero_MovesUp()
    {
        // Heading 0 = up (negative Y), per GetVelocity (heading - 90 → cos/sin).
        var sprite = new Sprite2D { CenterX = 0, CenterY = 0, Heading = 0f, Speed = 100f };
        sprite.Update(Context(0));

        sprite.Update(Context(1.0));
        Assert.Equal(0f, sprite.CenterX, 3);
        Assert.Equal(-100f, sprite.CenterY, 3);
    }

    [Fact]
    public void Update_Heading90_MovesRight()
    {
        var sprite = new Sprite2D { CenterX = 0, CenterY = 0, Heading = 90f, Speed = 50f };
        sprite.Update(Context(0));

        sprite.Update(Context(2.0));
        Assert.Equal(100f, sprite.CenterX, 3);
        Assert.Equal(0f, sprite.CenterY, 3);
    }

    [Fact]
    public void Update_Heading180_MovesDown()
    {
        var sprite = new Sprite2D { CenterX = 0, CenterY = 0, Heading = 180f, Speed = 10f };
        sprite.Update(Context(0));

        sprite.Update(Context(1.0));
        Assert.Equal(0f, sprite.CenterX, 3);
        Assert.Equal(10f, sprite.CenterY, 3);
    }

    [Fact]
    public void Update_AccumulatesRotation_AndWrapsAt360()
    {
        var sprite = new Sprite2D { Rotation = 350f, RotationSpeed = 30f };
        sprite.Update(Context(0));

        sprite.Update(Context(1.0)); // +30° → 380 % 360 → 20
        Assert.Equal(20f, sprite.Rotation, 3);
    }

    [Fact]
    public void Update_ReturnsTrue_WhenPositionChanges()
    {
        var sprite = new Sprite2D { Heading = 90f, Speed = 1f };
        sprite.Update(Context(0));            // first-frame change
        Assert.True(sprite.Update(Context(1.0))); // moved
    }

    [Theory]
    [InlineData(0f,   0f,    -1f)]   // up
    [InlineData(90f,  1f,    0f)]    // right
    [InlineData(180f, 0f,    1f)]    // down
    [InlineData(270f, -1f,   0f)]    // left
    public void GetVelocity_MatchesCardinalDirections(float heading, float expectedVx, float expectedVy)
    {
        var (vx, vy) = Sprite2D.GetVelocity(1f, heading);
        Assert.Equal(expectedVx, vx, 3);
        Assert.Equal(expectedVy, vy, 3);
    }

    [Fact]
    public void GetVelocity_ScalesWithSpeed()
    {
        var (vx, vy) = Sprite2D.GetVelocity(25f, 90f);
        Assert.Equal(25f, vx, 3);
        Assert.Equal(0f, vy, 3);
    }

    [Theory]
    [InlineData(1f, 0f, 1f, 90f)]    // east
    [InlineData(0f, 1f, 1f, 180f)]   // south
    [InlineData(-1f, 0f, 1f, 270f)]  // west
    [InlineData(0f, -1f, 1f, 0f)]    // north
    public void GetSpeedAndHeading_RecoversCardinals(float vx, float vy, float expectedSpeed, float expectedHeading)
    {
        var (speed, heading) = Sprite2D.GetSpeedAndHeading(vx, vy);
        Assert.Equal(expectedSpeed, speed, 3);
        Assert.Equal(expectedHeading, heading, 3);
    }

    [Theory]
    [InlineData(45f,   30f)]
    [InlineData(123.4f, 5f)]
    [InlineData(270f,  100f)]
    public void GetVelocity_And_GetSpeedAndHeading_AreInverses(float heading, float speed)
    {
        var (vx, vy) = Sprite2D.GetVelocity(speed, heading);
        var (s, h)   = Sprite2D.GetSpeedAndHeading(vx, vy);
        Assert.Equal(speed, s, 2);
        Assert.Equal(heading, h, 2);
    }

    [Fact]
    public void ChangeVelocity_AppliesTransform()
    {
        var sprite = new Sprite2D { Heading = 90f, Speed = 10f };
        // Negate velocity → reversed heading, same speed.
        sprite.ChangeVelocity((vx, vy) => (-vx, -vy));
        Assert.Equal(10f, sprite.Speed, 2);
        Assert.Equal(270f, sprite.Heading, 2);
    }
}

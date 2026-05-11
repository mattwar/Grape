using System.Numerics;

namespace Blitter.Tests;

public class KeyboardDirectionTests
{
    private const float Eps = 1e-5f;

    private static (bool[] prev, bool[] curr) MakeState() =>
        (new bool[512], new bool[512]);

    [Fact]
    public void Direction_NeitherHeld_ReturnsZero()
    {
        var (prev, curr) = MakeState();
        Keyboard.SetTestSnapshot(prev, curr);
        Assert.Equal(0f, Keyboard.Direction(PhysicalKey.A, PhysicalKey.D));
    }

    [Fact]
    public void Direction_PositiveHeld_ReturnsPlusOne()
    {
        var (prev, curr) = MakeState();
        curr[(int)PhysicalKey.D] = true;
        Keyboard.SetTestSnapshot(prev, curr);
        Assert.Equal(1f, Keyboard.Direction(PhysicalKey.A, PhysicalKey.D));
    }

    [Fact]
    public void Direction_NegativeHeld_ReturnsMinusOne()
    {
        var (prev, curr) = MakeState();
        curr[(int)PhysicalKey.A] = true;
        Keyboard.SetTestSnapshot(prev, curr);
        Assert.Equal(-1f, Keyboard.Direction(PhysicalKey.A, PhysicalKey.D));
    }

    [Fact]
    public void Direction_BothHeld_CancelsToZero()
    {
        var (prev, curr) = MakeState();
        curr[(int)PhysicalKey.A] = true;
        curr[(int)PhysicalKey.D] = true;
        Keyboard.SetTestSnapshot(prev, curr);
        Assert.Equal(0f, Keyboard.Direction(PhysicalKey.A, PhysicalKey.D));
    }

    [Fact]
    public void Direction2D_NoneHeld_ReturnsZero()
    {
        var (prev, curr) = MakeState();
        Keyboard.SetTestSnapshot(prev, curr);
        var v = Keyboard.Direction2D(
            PhysicalKey.A, PhysicalKey.D,
            PhysicalKey.S, PhysicalKey.W);
        Assert.Equal(Vector2.Zero, v);
    }

    [Fact]
    public void Direction2D_CardinalsAreUnitLength()
    {
        var (prev, curr) = MakeState();
        curr[(int)PhysicalKey.D] = true;
        Keyboard.SetTestSnapshot(prev, curr);
        var v = Keyboard.Direction2D(
            PhysicalKey.A, PhysicalKey.D,
            PhysicalKey.S, PhysicalKey.W);
        Assert.Equal(new Vector2(1f, 0f), v);
        Assert.Equal(1f, v.Length(), 5);
    }

    [Fact]
    public void Direction2D_DiagonalIsNormalized()
    {
        var (prev, curr) = MakeState();
        curr[(int)PhysicalKey.D] = true;
        curr[(int)PhysicalKey.W] = true;
        Keyboard.SetTestSnapshot(prev, curr);
        var v = Keyboard.Direction2D(
            PhysicalKey.A, PhysicalKey.D,
            PhysicalKey.S, PhysicalKey.W);
        Assert.Equal(1f, v.Length(), 5);
        var expected = MathF.Sqrt(0.5f);
        Assert.InRange(v.X, expected - Eps, expected + Eps);
        Assert.InRange(v.Y, expected - Eps, expected + Eps);
    }

    [Fact]
    public void Direction2D_OpposingPairsCancel()
    {
        var (prev, curr) = MakeState();
        curr[(int)PhysicalKey.A] = true;
        curr[(int)PhysicalKey.D] = true;
        curr[(int)PhysicalKey.W] = true;
        curr[(int)PhysicalKey.S] = true;
        Keyboard.SetTestSnapshot(prev, curr);
        var v = Keyboard.Direction2D(
            PhysicalKey.A, PhysicalKey.D,
            PhysicalKey.S, PhysicalKey.W);
        Assert.Equal(Vector2.Zero, v);
    }
}

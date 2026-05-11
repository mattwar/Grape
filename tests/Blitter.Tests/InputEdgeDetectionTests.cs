using System.Numerics;

namespace Blitter.Tests;

public class InputEdgeDetectionTests
{
    [Fact]
    public void Keyboard_WasJustPressed_OnlyOnRisingEdge()
    {
        var prev = new bool[512];
        var curr = new bool[512];
        var idx = (int)PhysicalKey.Space;

        // Up -> Down
        prev[idx] = false;
        curr[idx] = true;
        Keyboard.SetTestSnapshot(prev, curr);
        Assert.True(Keyboard.WasJustPressed(PhysicalKey.Space));
        Assert.False(Keyboard.WasJustReleased(PhysicalKey.Space));

        // Held: Down -> Down
        prev = (bool[])curr.Clone();
        Keyboard.SetTestSnapshot(prev, curr);
        Assert.False(Keyboard.WasJustPressed(PhysicalKey.Space));
        Assert.False(Keyboard.WasJustReleased(PhysicalKey.Space));

        // Down -> Up
        prev = (bool[])curr.Clone();
        curr[idx] = false;
        Keyboard.SetTestSnapshot(prev, curr);
        Assert.False(Keyboard.WasJustPressed(PhysicalKey.Space));
        Assert.True(Keyboard.WasJustReleased(PhysicalKey.Space));
    }

    [Fact]
    public void Keyboard_OutOfRangeKeyReturnsFalse()
    {
        // Empty arrays: any key index is out of range.
        Keyboard.SetTestSnapshot(Array.Empty<bool>(), Array.Empty<bool>());
        Assert.False(Keyboard.WasJustPressed(PhysicalKey.Space));
        Assert.False(Keyboard.WasJustReleased(PhysicalKey.Space));
    }

    [Fact]
    public void Mouse_WasJustPressed_OnlyOnRisingEdge()
    {
        // Up -> Down
        Mouse.SetTestSnapshot(
            previousButtons: MouseButtons.None,
            currentButtons: MouseButtons.Left,
            previousPosition: Vector2.Zero,
            currentPosition: Vector2.Zero);
        Assert.True(Mouse.WasJustPressed(MouseButton.Left));
        Assert.False(Mouse.WasJustReleased(MouseButton.Left));

        // Held
        Mouse.SetTestSnapshot(
            MouseButtons.Left, MouseButtons.Left,
            Vector2.Zero, Vector2.Zero);
        Assert.False(Mouse.WasJustPressed(MouseButton.Left));
        Assert.False(Mouse.WasJustReleased(MouseButton.Left));

        // Down -> Up
        Mouse.SetTestSnapshot(
            MouseButtons.Left, MouseButtons.None,
            Vector2.Zero, Vector2.Zero);
        Assert.False(Mouse.WasJustPressed(MouseButton.Left));
        Assert.True(Mouse.WasJustReleased(MouseButton.Left));
    }

    [Fact]
    public void Mouse_DistinguishesButtons()
    {
        Mouse.SetTestSnapshot(
            MouseButtons.Left, MouseButtons.Left | MouseButtons.Right,
            Vector2.Zero, Vector2.Zero);
        Assert.False(Mouse.WasJustPressed(MouseButton.Left));
        Assert.True(Mouse.WasJustPressed(MouseButton.Right));
        Assert.False(Mouse.WasJustPressed(MouseButton.Middle));
    }

    [Fact]
    public void Mouse_Delta_IsCurrentMinusPrevious()
    {
        Mouse.SetTestSnapshot(
            MouseButtons.None, MouseButtons.None,
            new Vector2(100, 200), new Vector2(110, 195));
        Assert.Equal(new Vector2(10, -5), Mouse.Delta);
    }
}

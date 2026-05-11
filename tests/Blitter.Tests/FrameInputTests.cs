using System.Numerics;

namespace Blitter.Tests;

public class FrameInputTests
{
    private static (FrameInput input, FakeInputSource source) Make()
    {
        var source = new FakeInputSource();
        // Cast required to reach internal constructor — tests have IVT access.
        var input = (FrameInput)Activator.CreateInstance(
            typeof(FrameInput),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            binder: null,
            args: new object[] { source },
            culture: null)!;
        return (input, source);
    }

    // ---------- Keyboard edges ----------

    [Fact]
    public void Keyboard_WasJustPressed_OnlyOnRisingEdge()
    {
        var (input, src) = Make();
        var idx = (int)PhysicalKey.Space;

        // First Update seeds baseline (key down at startup must NOT fire edge).
        src.Keys[idx] = true;
        input.Update();
        Assert.False(input.WasJustPressed(PhysicalKey.Space));
        Assert.True(input.IsDown(PhysicalKey.Space));

        // Held: still no edge.
        input.Update();
        Assert.False(input.WasJustPressed(PhysicalKey.Space));

        // Down -> Up
        src.Keys[idx] = false;
        input.Update();
        Assert.True(input.WasJustReleased(PhysicalKey.Space));

        // Up -> Down emits rising edge.
        src.Keys[idx] = true;
        input.Update();
        Assert.True(input.WasJustPressed(PhysicalKey.Space));
        Assert.False(input.WasJustReleased(PhysicalKey.Space));
    }

    [Fact]
    public void Keyboard_OutOfRangeKeyReturnsFalse()
    {
        var (input, src) = Make();
        src.Keys = Array.Empty<bool>();
        input.Update();
        Assert.False(input.WasJustPressed(PhysicalKey.Space));
        Assert.False(input.WasJustReleased(PhysicalKey.Space));
        Assert.False(input.IsDown(PhysicalKey.Space));
    }

    [Fact]
    public void Independent_FrameInputs_Have_Independent_Edges()
    {
        var src = new FakeInputSource();
        var a = (FrameInput)Activator.CreateInstance(typeof(FrameInput),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, new object[] { src }, null)!;
        var b = (FrameInput)Activator.CreateInstance(typeof(FrameInput),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, new object[] { src }, null)!;

        // Both seed with Space=up.
        a.Update();
        b.Update();
        // Space pressed.
        src.Keys[(int)PhysicalKey.Space] = true;
        // A advances; B does not.
        a.Update();
        Assert.True(a.WasJustPressed(PhysicalKey.Space));
        Assert.False(b.WasJustPressed(PhysicalKey.Space));

        // Now B advances and sees its own first rising edge.
        b.Update();
        Assert.True(b.WasJustPressed(PhysicalKey.Space));
        // A's next Update with no change clears the edge.
        a.Update();
        Assert.False(a.WasJustPressed(PhysicalKey.Space));
    }

    // ---------- Keyboard direction ----------

    [Fact]
    public void Direction_NeitherHeld_ReturnsZero()
    {
        var (input, _) = Make();
        input.Update();
        Assert.Equal(0f, input.Direction(PhysicalKey.A, PhysicalKey.D));
    }

    [Fact]
    public void Direction_PositiveHeld_ReturnsPlusOne()
    {
        var (input, src) = Make();
        src.Keys[(int)PhysicalKey.D] = true;
        input.Update();
        Assert.Equal(1f, input.Direction(PhysicalKey.A, PhysicalKey.D));
    }

    [Fact]
    public void Direction_NegativeHeld_ReturnsMinusOne()
    {
        var (input, src) = Make();
        src.Keys[(int)PhysicalKey.A] = true;
        input.Update();
        Assert.Equal(-1f, input.Direction(PhysicalKey.A, PhysicalKey.D));
    }

    [Fact]
    public void Direction_BothHeld_CancelsToZero()
    {
        var (input, src) = Make();
        src.Keys[(int)PhysicalKey.A] = true;
        src.Keys[(int)PhysicalKey.D] = true;
        input.Update();
        Assert.Equal(0f, input.Direction(PhysicalKey.A, PhysicalKey.D));
    }

    [Fact]
    public void Direction2D_NoneHeld_ReturnsZero()
    {
        var (input, _) = Make();
        input.Update();
        var v = input.Direction2D(
            PhysicalKey.A, PhysicalKey.D,
            PhysicalKey.S, PhysicalKey.W);
        Assert.Equal(Vector2.Zero, v);
    }

    [Fact]
    public void Direction2D_CardinalsAreUnitLength()
    {
        var (input, src) = Make();
        src.Keys[(int)PhysicalKey.D] = true;
        input.Update();
        var v = input.Direction2D(
            PhysicalKey.A, PhysicalKey.D,
            PhysicalKey.S, PhysicalKey.W);
        Assert.Equal(new Vector2(1f, 0f), v);
    }

    [Fact]
    public void Direction2D_DiagonalIsNormalized()
    {
        var (input, src) = Make();
        src.Keys[(int)PhysicalKey.D] = true;
        src.Keys[(int)PhysicalKey.W] = true;
        input.Update();
        var v = input.Direction2D(
            PhysicalKey.A, PhysicalKey.D,
            PhysicalKey.S, PhysicalKey.W);
        Assert.Equal(1f, v.Length(), 5);
        var expected = MathF.Sqrt(0.5f);
        Assert.InRange(v.X, expected - 1e-5f, expected + 1e-5f);
        Assert.InRange(v.Y, expected - 1e-5f, expected + 1e-5f);
    }

    [Fact]
    public void Direction2D_OpposingPairsCancel()
    {
        var (input, src) = Make();
        src.Keys[(int)PhysicalKey.A] = true;
        src.Keys[(int)PhysicalKey.D] = true;
        src.Keys[(int)PhysicalKey.W] = true;
        src.Keys[(int)PhysicalKey.S] = true;
        input.Update();
        var v = input.Direction2D(
            PhysicalKey.A, PhysicalKey.D,
            PhysicalKey.S, PhysicalKey.W);
        Assert.Equal(Vector2.Zero, v);
    }

    // ---------- Mouse edges ----------

    [Fact]
    public void Mouse_WasJustPressed_OnlyOnRisingEdge()
    {
        var (input, src) = Make();
        // Seed: no buttons.
        input.Update();
        // Press left.
        src.MouseButtons = MouseButtons.Left;
        input.Update();
        Assert.True(input.WasJustPressed(MouseButton.Left));
        Assert.False(input.WasJustReleased(MouseButton.Left));

        // Held.
        input.Update();
        Assert.False(input.WasJustPressed(MouseButton.Left));
        Assert.False(input.WasJustReleased(MouseButton.Left));

        // Release.
        src.MouseButtons = MouseButtons.None;
        input.Update();
        Assert.False(input.WasJustPressed(MouseButton.Left));
        Assert.True(input.WasJustReleased(MouseButton.Left));
    }

    [Fact]
    public void Mouse_DistinguishesButtons()
    {
        var (input, src) = Make();
        src.MouseButtons = MouseButtons.Left;
        input.Update();
        src.MouseButtons = MouseButtons.Left | MouseButtons.Right;
        input.Update();
        Assert.False(input.WasJustPressed(MouseButton.Left));
        Assert.True(input.WasJustPressed(MouseButton.Right));
        Assert.False(input.WasJustPressed(MouseButton.Middle));
    }

    [Fact]
    public void Mouse_Delta_IsCurrentMinusPrevious_WhenNotRelative()
    {
        var (input, src) = Make();
        src.MousePosition = new Vector2(100, 200);
        input.Update();
        src.MousePosition = new Vector2(110, 195);
        input.Update();
        Assert.Equal(new Vector2(10, -5), input.MouseDelta);
    }

    [Fact]
    public void Mouse_Delta_UsesRelativeMotion_WhenAnyWindowRelative()
    {
        var (input, src) = Make();
        src.AnyWindowRelative = true;
        src.RelativeMouseMotion = new Vector2(42, -7);
        input.Update();
        // First call seeds positions; subsequent call returns the
        // drained relative motion regardless of position delta.
        src.MousePosition = new Vector2(999, 999);
        src.RelativeMouseMotion = new Vector2(3, 4);
        input.Update();
        Assert.Equal(new Vector2(3, 4), input.MouseDelta);
    }
}

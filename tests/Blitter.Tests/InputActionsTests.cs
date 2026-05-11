using System.Numerics;
using System.Reflection;
using Blitter.Bits;

namespace Blitter.Tests;

public class InputActionsTests
{
    private static (InputActions actions, FrameInput input, FakeInputSource src) Setup()
    {
        var src = new FakeInputSource();
        var input = (FrameInput)Activator.CreateInstance(
            typeof(FrameInput),
            BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object[] { src }, null)!;
        var actions = new InputActions(input);
        return (actions, input, src);
    }

    // ---------- Digital ----------

    [Fact]
    public void Digital_IsPressed_TracksHeld()
    {
        var (a, input, src) = Setup();
        a.Bind("Jump", Key.Space);
        input.Update();
        Assert.False(a.IsPressed("Jump"));

        src.Keys[(int)PhysicalKey.Space] = true;
        input.Update();
        Assert.True(a.IsPressed("Jump"));
    }

    [Fact]
    public void Digital_WasJustPressed_FiresOnceOnRise()
    {
        var (a, input, src) = Setup();
        a.Bind("Jump", Key.Space);
        input.Update();
        src.Keys[(int)PhysicalKey.Space] = true;
        input.Update();
        Assert.True(a.WasJustPressed("Jump"));
        input.Update();
        Assert.False(a.WasJustPressed("Jump"));
    }

    [Fact]
    public void Digital_MultipleBindings_OredTogether_NoDoubleFire()
    {
        var (a, input, src) = Setup();
        a.Bind("Jump", Key.Space);
        a.Bind("Jump", Key.W);
        input.Update();

        // Press Space — rising edge.
        src.Keys[(int)PhysicalKey.Space] = true;
        input.Update();
        Assert.True(a.WasJustPressed("Jump"));

        // Then also press W while Space still held — action was already
        // pressed, so this must NOT fire WasJustPressed again.
        src.Keys[(int)PhysicalKey.W] = true;
        input.Update();
        Assert.False(a.WasJustPressed("Jump"));
        Assert.True(a.IsPressed("Jump"));

        // Release Space, W still held — action stays pressed; no edge.
        src.Keys[(int)PhysicalKey.Space] = false;
        input.Update();
        Assert.False(a.WasJustPressed("Jump"));
        Assert.False(a.WasJustReleased("Jump"));
        Assert.True(a.IsPressed("Jump"));

        // Release W — action transitions from pressed to released.
        src.Keys[(int)PhysicalKey.W] = false;
        input.Update();
        Assert.True(a.WasJustReleased("Jump"));
    }

    [Fact]
    public void Digital_MouseButtonBinding()
    {
        var (a, input, src) = Setup();
        a.Bind("Fire", MouseButton.Left);
        input.Update();
        src.MouseButtons = MouseButtons.Left;
        input.Update();
        Assert.True(a.WasJustPressed("Fire"));
        Assert.True(a.IsPressed("Fire"));
    }

    [Fact]
    public void Digital_PhysicalKeyBinding()
    {
        var (a, input, src) = Setup();
        a.Bind("Walk", PhysicalKey.W);
        input.Update();
        src.Keys[(int)PhysicalKey.W] = true;
        input.Update();
        Assert.True(a.WasJustPressed("Walk"));
    }

    // ---------- Direction ----------

    [Fact]
    public void Direction_KeyPairBinding()
    {
        var (a, input, src) = Setup();
        a.BindDirection("Strafe", Key.A, Key.D);
        input.Update();
        Assert.Equal(0f, a.GetDirection("Strafe"));

        src.Keys[(int)PhysicalKey.D] = true;
        input.Update();
        Assert.Equal(1f, a.GetDirection("Strafe"));

        src.Keys[(int)PhysicalKey.D] = false;
        src.Keys[(int)PhysicalKey.A] = true;
        input.Update();
        Assert.Equal(-1f, a.GetDirection("Strafe"));
    }

    [Fact]
    public void Direction2D_KeyQuadBinding()
    {
        var (a, input, src) = Setup();
        a.BindDirection2D("Move", Key.A, Key.D, Key.S, Key.W);
        input.Update();
        Assert.Equal(Vector2.Zero, a.GetDirection2D("Move"));

        src.Keys[(int)PhysicalKey.D] = true;
        input.Update();
        Assert.Equal(new Vector2(1, 0), a.GetDirection2D("Move"));

        src.Keys[(int)PhysicalKey.W] = true;
        input.Update();
        var v = a.GetDirection2D("Move");
        Assert.Equal(1f, v.Length(), 5);
    }

    // ---------- Kind enforcement ----------

    [Fact]
    public void MixingKinds_OnSameAction_Throws()
    {
        var (a, _, _) = Setup();
        a.Bind("X", Key.Space);
        Assert.Throws<InvalidOperationException>(
            () => a.BindDirection("X", Key.A, Key.D));
    }

    [Fact]
    public void WrongKindQuery_Throws()
    {
        var (a, _, _) = Setup();
        a.Bind("Jump", Key.Space);
        Assert.Throws<InvalidOperationException>(() => a.GetDirection("Jump"));
        Assert.Throws<InvalidOperationException>(() => a.GetDirection2D("Jump"));

        a.BindDirection("Strafe", Key.A, Key.D);
        Assert.Throws<InvalidOperationException>(() => a.IsPressed("Strafe"));
    }

    [Fact]
    public void UnknownAction_Throws_KeyNotFound()
    {
        var (a, _, _) = Setup();
        Assert.Throws<KeyNotFoundException>(() => a.IsPressed("Ghost"));
        Assert.Throws<KeyNotFoundException>(() => a.GetDirection("Ghost"));
        Assert.False(a.Contains("Ghost"));
    }

    [Fact]
    public void ActionNames_CaseInsensitive()
    {
        var (a, input, src) = Setup();
        a.Bind("Jump", Key.Space);
        src.Keys[(int)PhysicalKey.Space] = true;
        input.Update();
        Assert.True(a.IsPressed("jump"));
        Assert.True(a.IsPressed("JUMP"));
    }

    // ---------- Rebind / Clear ----------

    [Fact]
    public void Rebind_ReplacesAll()
    {
        var (a, _, _) = Setup();
        a.Bind("Jump", Key.Space);
        a.Bind("Jump", Key.W);
        Assert.Equal(2, a.GetBindings("Jump").Count);
        a.Rebind("Jump", Key.Return);
        Assert.Single(a.GetBindings("Jump"));
        Assert.Equal(new KeyBinding(Key.Return), a.GetBindings("Jump")[0]);
    }

    [Fact]
    public void Rebind_CanChangeKind()
    {
        var (a, _, _) = Setup();
        a.Bind("X", Key.Space);
        Assert.Equal(InputActionKind.Digital, a.GetKind("X"));
        a.RebindDirection("X", Key.A, Key.D);
        Assert.Equal(InputActionKind.Direction, a.GetKind("X"));
    }

    [Fact]
    public void Clear_RemovesAction()
    {
        var (a, _, _) = Setup();
        a.Bind("Jump", Key.Space);
        a.Clear("Jump");
        Assert.False(a.Contains("Jump"));
        Assert.Throws<KeyNotFoundException>(() => a.IsPressed("Jump"));
    }

    // ---------- JSON ----------

    [Fact]
    public void Json_RoundTripsAllBindingKinds()
    {
        var (a, input, _) = Setup();
        a.Bind("Jump", Key.Space, Key.W);
        a.Bind("Walk", PhysicalKey.LShift);
        a.Bind("Fire", MouseButton.Left);
        a.BindDirection("Strafe", Key.A, Key.D);
        a.BindDirection2D("Move", Key.A, Key.D, Key.S, Key.W);

        var json = a.ToJson();
        var restored = InputActions.FromJson(json, input);

        Assert.Equal(InputActionKind.Digital, restored.GetKind("Jump"));
        Assert.Equal(2, restored.GetBindings("Jump").Count);

        Assert.Equal(InputActionKind.Digital, restored.GetKind("Walk"));
        Assert.IsType<PhysicalKeyBinding>(restored.GetBindings("Walk")[0]);

        Assert.Equal(InputActionKind.Digital, restored.GetKind("Fire"));
        Assert.IsType<MouseButtonBinding>(restored.GetBindings("Fire")[0]);

        Assert.Equal(InputActionKind.Direction, restored.GetKind("Strafe"));
        var sd = Assert.IsType<KeyDirectionBinding>(restored.GetBindings("Strafe")[0]);
        Assert.Equal(Key.A, sd.Negative);
        Assert.Equal(Key.D, sd.Positive);

        Assert.Equal(InputActionKind.Direction2D, restored.GetKind("Move"));
        var md = Assert.IsType<KeyDirection2DBinding>(restored.GetBindings("Move")[0]);
        Assert.Equal(Key.W, md.Up);
    }

    [Fact]
    public void Json_UnknownBindingType_Throws()
    {
        var (_, input, _) = Setup();
        var bad = "{ \"X\": { \"kind\": \"digital\", \"bindings\": [ { \"type\": \"alien\", \"key\": \"A\" } ] } }";
        Assert.Throws<FormatException>(() => InputActions.FromJson(bad, input));
    }
}

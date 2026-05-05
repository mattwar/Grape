namespace Grape.Jelly.Tests;

public class SceneTests
{
    private sealed class FakeProp : Prop
    {
        public bool ShouldReportChanged { get; set; }
        public int UpdateCount { get; private set; }
        public int RenderCount { get; private set; }

        public override bool Update(in UpdateContext context)
        {
            UpdateCount++;
            return ShouldReportChanged;
        }

        public override void Render(Renderer2D renderer)
        {
            RenderCount++;
        }
    }

    [Fact]
    public void Update_VisitsEveryProp()
    {
        var a = new FakeProp();
        var b = new FakeProp();
        var scene = new Scene(ImmutableList.Create<Prop>(a, b));

        scene.Update(new UpdateContext());

        Assert.Equal(1, a.UpdateCount);
        Assert.Equal(1, b.UpdateCount);
    }

    [Fact]
    public void Update_ReturnsTrue_IfAnyPropChanged()
    {
        var a = new FakeProp { ShouldReportChanged = false };
        var b = new FakeProp { ShouldReportChanged = true };
        var scene = new Scene(ImmutableList.Create<Prop>(a, b));

        Assert.True(scene.Update(new UpdateContext()));
    }

    [Fact]
    public void Update_ReturnsFalse_IfNoPropChanged()
    {
        var a = new FakeProp { ShouldReportChanged = false };
        var scene = new Scene(ImmutableList.Create<Prop>(a));

        Assert.False(scene.Update(new UpdateContext()));
    }

    [Fact]
    public void Update_StopsEarly_WhenCancelled()
    {
        var a = new FakeProp { ShouldReportChanged = true };
        var b = new FakeProp { ShouldReportChanged = true };
        var scene = new Scene(ImmutableList.Create<Prop>(a, b));

        var cts = new CancellationTokenSource();
        cts.Cancel();
        var changed = scene.Update(new UpdateContext(), cts.Token);

        Assert.False(changed);
        Assert.Equal(0, a.UpdateCount);
        Assert.Equal(0, b.UpdateCount);
    }

    [Fact]
    public void AddProp_ExposesAddedPropToFutureUpdates()
    {
        var initial = new FakeProp();
        var scene = new Scene(ImmutableList.Create<Prop>(initial));

        var added = new FakeProp();
        scene.AddProp(added);

        scene.Update(new UpdateContext());

        Assert.Equal(1, initial.UpdateCount);
        Assert.Equal(1, added.UpdateCount);
    }
}

namespace Blitter.Blocks.Tests;

public class SceneTests
{
    private sealed class FakeProp : Prop2D
    {
        public bool ShouldReportChanged { get; set; }
        public int UpdateCount { get; private set; }
        public int RenderCount { get; private set; }

        public override bool Update(in UpdateContext2D context)
        {
            UpdateCount++;
            return ShouldReportChanged;
        }

        public override void Draw(Renderer2D renderer)
        {
            RenderCount++;
        }
    }

    [Fact]
    public void Update_VisitsEveryProp()
    {
        var a = new FakeProp();
        var b = new FakeProp();
        var scene = new Scene2D(ImmutableList.Create<Prop2D>(a, b));

        scene.Update(new UpdateContext2D());

        Assert.Equal(1, a.UpdateCount);
        Assert.Equal(1, b.UpdateCount);
    }

    [Fact]
    public void Update_ReturnsTrue_IfAnyPropChanged()
    {
        var a = new FakeProp { ShouldReportChanged = false };
        var b = new FakeProp { ShouldReportChanged = true };
        var scene = new Scene2D(ImmutableList.Create<Prop2D>(a, b));

        Assert.True(scene.Update(new UpdateContext2D()));
    }

    [Fact]
    public void Update_ReturnsFalse_IfNoPropChanged()
    {
        var a = new FakeProp { ShouldReportChanged = false };
        var scene = new Scene2D(ImmutableList.Create<Prop2D>(a));

        Assert.False(scene.Update(new UpdateContext2D()));
    }

    [Fact]
    public void Add_ExposesAddedPropToFutureUpdates()
    {
        var initial = new FakeProp();
        var scene = new Scene2D(ImmutableList.Create<Prop2D>(initial));

        var added = new FakeProp();
        scene.Add(added);

        scene.Update(new UpdateContext2D());

        Assert.Equal(1, initial.UpdateCount);
        Assert.Equal(1, added.UpdateCount);
    }
}

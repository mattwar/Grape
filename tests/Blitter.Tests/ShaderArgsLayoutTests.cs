using Blitter.Shaders;

namespace Blitter.Tests;

public class ShaderArgsLayoutTests
{
    [Theory]
    [InlineData(ShaderArgKind.Float,     4)]
    [InlineData(ShaderArgKind.Float2,    8)]
    [InlineData(ShaderArgKind.Float3,    12)]
    [InlineData(ShaderArgKind.Float4,    16)]
    [InlineData(ShaderArgKind.Int,       4)]
    [InlineData(ShaderArgKind.UInt,      4)]
    [InlineData(ShaderArgKind.Matrix4x4, 64)]
    public void ShaderArgElement_Size_MatchesKind(ShaderArgKind kind, int expectedSize)
    {
        var element = new ShaderArgElement(ShaderArgStage.Vertex, 0, kind);
        Assert.Equal(expectedSize, element.Size);
    }

    [Fact]
    public void TotalSize_IsZero_WhenEmpty()
    {
        var layout = new ShaderArgsLayout();
        Assert.Equal(0, layout.TotalSize);
    }

    [Fact]
    public void TotalSize_SumsElementSizes()
    {
        var layout = new ShaderArgsLayout(
            new ShaderArgElement(ShaderArgStage.Vertex,   0, ShaderArgKind.Matrix4x4), // 64
            new ShaderArgElement(ShaderArgStage.Fragment, 0, ShaderArgKind.Float4),    // 16
            new ShaderArgElement(ShaderArgStage.Fragment, 1, ShaderArgKind.Float));    // 4
        Assert.Equal(84, layout.TotalSize);
    }

    [Fact]
    public void ParamsConstructor_PopulatesElementsInOrder()
    {
        var e0 = new ShaderArgElement(ShaderArgStage.Vertex,   0, ShaderArgKind.Matrix4x4);
        var e1 = new ShaderArgElement(ShaderArgStage.Fragment, 0, ShaderArgKind.Float4);
        var layout = new ShaderArgsLayout(e0, e1);

        Assert.Equal(2, layout.Elements.Length);
        Assert.Equal(e0, layout.Elements[0]);
        Assert.Equal(e1, layout.Elements[1]);
    }
}

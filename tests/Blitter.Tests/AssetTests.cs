using Blitter.Bits;

namespace Blitter.Tests;

public class AssetTests
{
    [Fact]
    public void RelativeToCaller_ResolvesNextToSourceFile()
    {
        var p = Asset.RelativeToCaller("hello.txt");
        var dir = Path.GetDirectoryName(p);
        Assert.NotNull(dir);
        // The test file lives next to AssetTests.cs in the test project,
        // so the resolved directory must contain it.
        Assert.True(File.Exists(Path.Combine(dir!, "AssetTests.cs")));
        Assert.Equal("hello.txt", Path.GetFileName(p));
    }

    [Fact]
    public void RelativeToCaller_NullName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Asset.RelativeToCaller(null!));
    }
}

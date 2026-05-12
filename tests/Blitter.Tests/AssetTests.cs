using Blitter.Bits;

namespace Blitter.Tests;

public class AssetTests
{
    [Fact]
    public void GetPathRelativeToCaller_ResolvesNextToSourceFile()
    {
        var resolved = Asset.GetPathRelativeToCaller("hello.txt");
        var expectedDir = Path.GetDirectoryName(ThisFile());
        Assert.Equal(expectedDir, Path.GetDirectoryName(resolved));
        Assert.Equal("hello.txt", Path.GetFileName(resolved));
    }

    private static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    [Fact]
    public void GetPathRelativeToCaller_NullName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Asset.GetPathRelativeToCaller(null!));
    }
}

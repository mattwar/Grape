using System.Runtime.CompilerServices;

namespace Blitter.Bits;

/// <summary>
/// Helpers for resolving asset file paths in samples and small apps
/// where loose files live next to the source file that loads them.
/// </summary>
public static class Asset
{
    /// <summary>
    /// Resolves <paramref name="name"/> relative to the source file
    /// that calls this method. Lets a sample like <c>foo.cs</c> say
    /// <c>Bitmap.Load(Asset.GetPathRelativeToCaller("foo.png"))</c> regardless
    /// of the shell's current working directory.
    /// </summary>
    public static string GetPathRelativeToCaller(
        string name,
        [CallerFilePath] string sourcePath = "")
    {
        ArgumentNullException.ThrowIfNull(name);
        var dir = Path.GetDirectoryName(sourcePath)
            ?? throw new InvalidOperationException(
                "Caller source path is unavailable.");
        return Path.Combine(dir, name);
    }
}

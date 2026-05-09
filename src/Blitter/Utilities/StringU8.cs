using System.Runtime.CompilerServices;
using System.Text;

namespace Blitter;

public class StringU8
{
    private readonly byte[] _utf8Bytes;

    private StringU8(byte[] utf8Bytes)
    {
        System.Diagnostics.Debug.Assert(utf8Bytes[^1] == 0); // null terminated
        _utf8Bytes = utf8Bytes;
    }

    public ReadOnlySpan<byte> AsSpan() => _utf8Bytes.AsSpan();
    public static implicit operator StringU8(string str) => From(str);
    public static implicit operator ReadOnlySpan<byte>(StringU8 utf8Str) => utf8Str.AsSpan();
    public static implicit operator ReadOnlyMemory<byte>(StringU8 utf8Str) => utf8Str._utf8Bytes;

    private static ConditionalWeakTable<string, StringU8> _cache = new();

    public static StringU8 From(string text)
    {
        if (!_cache.TryGetValue(text, out var ustr))
        {
            var nullTerminatedBytes = Encoding.UTF8.GetBytes(text + '\0');
            ustr = _cache.GetValue(text, t => new StringU8(nullTerminatedBytes));
        }

        return ustr;
    }
}

public static class StringU8Extensions
{
    public static StringU8 ToUtf8(this string str) => StringU8.From(str);
}

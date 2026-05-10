using System.Runtime.CompilerServices;

namespace Blitter.Utilities;

/// <summary>
/// Interpolated-string handler used by <see cref="DebugDraw"/>'s text
/// methods to skip formatting work when no renderer is consuming.
/// </summary>
// When DebugDraw.IsActive is false, the C# compiler short-circuits the
// entire interpolation: AppendLiteral / AppendFormatted are never
// called, so no string is built and no boxing happens for value-type
// arguments. Cost in the disabled case is one volatile read.
[InterpolatedStringHandler]
public ref struct DebugTextInterpolatedStringHandler
{
    private DefaultInterpolatedStringHandler _inner;
    private readonly bool _enabled;

    public DebugTextInterpolatedStringHandler(
        int literalLength, int formattedCount, out bool shouldAppend)
    {
        _enabled = DebugDraw.IsActive;
        shouldAppend = _enabled;
        _inner = _enabled
            ? new DefaultInterpolatedStringHandler(literalLength, formattedCount)
            : default;
    }

    public void AppendLiteral(string value)
    {
        if (_enabled) _inner.AppendLiteral(value);
    }

    public void AppendFormatted<T>(T value)
    {
        if (_enabled) _inner.AppendFormatted(value);
    }

    public void AppendFormatted<T>(T value, string? format)
    {
        if (_enabled) _inner.AppendFormatted(value, format);
    }

    public void AppendFormatted<T>(T value, int alignment)
    {
        if (_enabled) _inner.AppendFormatted(value, alignment);
    }

    public void AppendFormatted<T>(T value, int alignment, string? format)
    {
        if (_enabled) _inner.AppendFormatted(value, alignment, format);
    }

    public void AppendFormatted(ReadOnlySpan<char> value)
    {
        if (_enabled) _inner.AppendFormatted(value);
    }

    public void AppendFormatted(string? value)
    {
        if (_enabled) _inner.AppendFormatted(value);
    }

    internal string ToStringAndClear() =>
        _enabled ? _inner.ToStringAndClear() : string.Empty;

    internal bool Enabled => _enabled;
}

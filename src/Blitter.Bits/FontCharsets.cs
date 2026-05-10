namespace Blitter.Bits;

/// <summary>
/// Reusable character-set strings for use as the <c>charset</c> argument
/// to <see cref="Font"/> constructors. Combine with string concatenation
/// to extend coverage, e.g.
/// <c>FontCharsets.AsciiPrintable + "áéíóúñ"</c>.
/// </summary>
public static class FontCharsets
{
    /// <summary>
    /// Printable ASCII (U+0020..U+007E): space through tilde, including
    /// digits, both letter cases, and punctuation. The default charset
    /// for <see cref="Font"/> when no <c>charset</c> argument is supplied.
    /// </summary>
    public const string AsciiPrintable =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

    /// <summary>Decimal digits only.</summary>
    public const string Digits = "0123456789";

    /// <summary>Uppercase Latin letters only.</summary>
    public const string UppercaseLatin = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>Lowercase Latin letters only.</summary>
    public const string LowercaseLatin = "abcdefghijklmnopqrstuvwxyz";
}

namespace Blitter;

/// <summary>
/// How aggressively to smooth out the staircase ("jaggies") seen along
/// triangle edges. Higher levels look better at the cost of memory and
/// fill-rate proportional to the sample count.
/// </summary>
/// <remarks>
/// MSAA only smooths geometry edges; it does not address aliasing
/// inside textured surfaces (use mipmaps for that) or shader-introduced
/// aliasing such as sparkly speculars.
/// </remarks>
public enum Antialiasing
{
    /// <summary>One sample per pixel. Fastest, but triangle silhouettes show staircase aliasing.</summary>
    None,

    /// <summary>2× MSAA. Modest improvement, modest cost.</summary>
    X2,

    /// <summary>4× MSAA. The universal sweet spot — visibly clean edges at moderate cost.</summary>
    X4,

    /// <summary>8× MSAA. Highest quality; bandwidth-heavy and may not be supported on every backend.</summary>
    X8,
}

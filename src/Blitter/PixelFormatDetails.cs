namespace Blitter;

/// <summary>
/// Describes the binary layout of pixels in a <see cref="PixelFormat"/>:
/// bit/byte width and the per-channel masks, bit counts, and shifts.
/// </summary>
public readonly record struct PixelFormatDetails
{
    /// <summary>The pixel format these details describe.</summary>
    public PixelFormat Format { get; init; }

    /// <summary>Total bits per pixel.</summary>
    public byte BitsPerPixel { get; init; }

    /// <summary>Total bytes per pixel.</summary>
    public byte BytesPerPixel { get; init; }

    /// <summary>Mask isolating the red channel.</summary>
    public uint RMask { get; init; }

    /// <summary>Mask isolating the green channel.</summary>
    public uint GMask { get; init; }

    /// <summary>Mask isolating the blue channel.</summary>
    public uint BMask { get; init; }

    /// <summary>Mask isolating the alpha channel.</summary>
    public uint AMask { get; init; }

    /// <summary>Number of bits in the red channel.</summary>
    public byte RBits { get; init; }

    /// <summary>Number of bits in the green channel.</summary>
    public byte GBits { get; init; }

    /// <summary>Number of bits in the blue channel.</summary>
    public byte BBits { get; init; }

    /// <summary>Number of bits in the alpha channel.</summary>
    public byte ABits { get; init; }

    /// <summary>Bit shift to extract the red channel.</summary>
    public byte RShift { get; init; }

    /// <summary>Bit shift to extract the green channel.</summary>
    public byte GShift { get; init; }

    /// <summary>Bit shift to extract the blue channel.</summary>
    public byte BShift { get; init; }

    /// <summary>Bit shift to extract the alpha channel.</summary>
    public byte AShift { get; init; }

    internal static PixelFormatDetails From(in SDL.PixelFormatDetails d) => new()
    {
        Format = (PixelFormat)d.Format,
        BitsPerPixel = d.BitsPerPixel,
        BytesPerPixel = d.BytesPerPixel,
        RMask = d.RMask,
        GMask = d.GMask,
        BMask = d.BMask,
        AMask = d.AMask,
        RBits = d.RBits,
        GBits = d.GBits,
        BBits = d.BBits,
        ABits = d.ABits,
        RShift = d.RShift,
        GShift = d.GShift,
        BShift = d.BShift,
        AShift = d.AShift,
    };
}

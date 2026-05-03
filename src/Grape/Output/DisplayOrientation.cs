namespace Grape;

/// <summary>
/// The orientation of a <see cref="Display"/>.
/// </summary>
/// <remarks>
/// Values mirror the underlying <c>SDL_DisplayOrientation</c> values so they
/// can be cast at the SDL boundary.
/// </remarks>
public enum DisplayOrientation
{
    /// <summary>The orientation could not be determined.</summary>
    Unknown = 0,

    /// <summary>Landscape with the right side up, relative to portrait.</summary>
    Landscape = 1,

    /// <summary>Landscape with the left side up, relative to portrait.</summary>
    LandscapeFlipped = 2,

    /// <summary>Portrait orientation.</summary>
    Portrait = 3,

    /// <summary>Portrait orientation, upside down.</summary>
    PortraitFlipped = 4,
}

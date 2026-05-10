namespace Blitter;

/// <summary>
/// How a renderer maps its logical drawing surface (set via
/// <see cref="Renderer2D.SetLogicalSize"/>) onto the actual render
/// target.
/// </summary>
public enum LogicalPresentation
{
    /// <summary>No logical presentation; draws use raw output pixels.</summary>
    Disabled = 0,

    /// <summary>Stretch the logical surface to fill the output, ignoring aspect ratio.</summary>
    Stretch = 1,

    /// <summary>Preserve aspect ratio; fill the smaller dimension and add bars on the larger one.</summary>
    Letterbox = 2,

    /// <summary>Preserve aspect ratio; fill the larger dimension and crop the smaller one.</summary>
    Overscan = 3,

    /// <summary>Like <see cref="Letterbox"/>, but only at integer scale factors (no fractional scaling).</summary>
    IntegerScale = 4,
}

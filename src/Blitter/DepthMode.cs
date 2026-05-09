namespace Blitter;

/// <summary>
/// Controls how a draw interacts with the depth buffer.
/// </summary>
public enum DepthMode
{
    /// <summary>
    /// Opaque 3D geometry: closer pixels win, and this draw can occlude
    /// later draws. The most common mode and the renderer's initial
    /// setting.
    /// </summary>
    Solid,

    /// <summary>
    /// Drawn only where it would be in front of existing solid geometry,
    /// but does not occlude later draws. Use for translucent or
    /// transparent objects so they layer correctly with each other.
    /// </summary>
    Transparent,

    /// <summary>
    /// Ignores the depth buffer entirely. Always draws, regardless of 3D
    /// position, and does not occlude anything. Use for HUDs and UI
    /// layered on top of a 3D scene.
    /// </summary>
    Overlay,
}

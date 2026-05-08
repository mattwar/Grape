namespace Grape;

/// <summary>
/// How to flip a sprite or image when rendering.
/// </summary>
public enum FlipMode
{
    None       = SDL.FlipMode.None,
    Horizontal = SDL.FlipMode.Horizontal,
    Vertical   = SDL.FlipMode.Vertical,
}

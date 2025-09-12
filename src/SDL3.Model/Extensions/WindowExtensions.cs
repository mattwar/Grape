namespace SDL3.Model;

public static class WindowExtensions
{
    /// <summary>
    /// Creates a <see cref="Texture"/>. Only <see cref="Texture"/> can be rendered.
    /// </summary>
    public static Texture CreateTexture(this Window window, int width, int height, SDL.PixelFormat? format = null, SDL.TextureAccess access = SDL.TextureAccess.Streaming)
    {
        return window.Renderer.CreateTexture(width, height, format ?? window.PixelFormat, access);
    }

    /// <summary>
    /// Creates a <see cref="Texture"/> the size of the window.
    /// </summary>
    public static Texture CreateTexture(this Window window)
    {
        var size = window.Size;
        return window.Renderer.CreateTexture(size.Width, size.Height, window.PixelFormat, SDL.TextureAccess.Streaming);
    }

    /// <summary>
    /// Creates a <see cref="Texture"/> from the specified <see cref="Surface"/>.
    /// </summary>
    public static Texture CreateTexture(this Window window, Surface surface)
    {
        return window.Renderer.CreateTexture(surface);
    }
}

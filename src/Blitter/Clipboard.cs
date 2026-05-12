namespace Blitter;

/// <summary>
/// Access to the system clipboard. All operations are UTF-8 friendly.
/// Calls are safe from any thread; they marshal to the application thread
/// internally.
/// </summary>
public static class Clipboard
{
    /// <summary>
    /// Gets or sets the plain UTF-8 text on the clipboard. Reads return an
    /// empty string if the clipboard does not contain text. Writes replace
    /// the current contents.
    /// </summary>
    public static string Text
    {
        get => Application.Current.Invoke(static () => SDL.GetClipboardText());
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Application.Current.Invoke(() =>
            {
                if (!SDL.SetClipboardText(value))
                    throw new InvalidOperationException(
                        $"Failed to set clipboard text: {SDL.GetError()}");
            });
        }
    }

    /// <summary>
    /// True if the clipboard currently contains a non-empty text string.
    /// </summary>
    public static bool HasText
        => Application.Current.Invoke(static () => SDL.HasClipboardText());

    /// <summary>
    /// True if the clipboard currently has data of the given MIME type.
    /// </summary>
    public static bool Has(string mimeType)
    {
        ArgumentException.ThrowIfNullOrEmpty(mimeType);
        return Application.Current.Invoke(() => SDL.HasClipboardData(mimeType));
    }

    /// <summary>
    /// The MIME types currently advertised by the clipboard.
    /// </summary>
    public static IReadOnlyList<string> MimeTypes
        => Application.Current.Invoke(static () =>
        {
            var types = SDL.GetClipboardMimeTypes(out _);
            return (IReadOnlyList<string>)(types ?? System.Array.Empty<string>());
        });

    /// <summary>
    /// Returns the clipboard data for the given MIME type, or null if there
    /// is no data of that type.
    /// </summary>
    public static byte[]? Get(string mimeType)
    {
        ArgumentException.ThrowIfNullOrEmpty(mimeType);
        return Application.Current.Invoke(() =>
        {
            var ptr = SDL.GetClipboardData(mimeType, out var size);
            if (ptr == 0)
                return null;
            try
            {
                var len = (int)size;
                if (len <= 0)
                    return System.Array.Empty<byte>();
                var buffer = new byte[len];
                System.Runtime.InteropServices.Marshal.Copy(ptr, buffer, 0, len);
                return buffer;
            }
            finally
            {
                SDL.Free(ptr);
            }
        });
    }

    /// <summary>
    /// Clears the clipboard contents.
    /// </summary>
    public static void Clear()
    {
        Application.Current.Invoke(static () =>
        {
            if (!SDL.ClearClipboardData())
                throw new InvalidOperationException(
                    $"Failed to clear clipboard: {SDL.GetError()}");
        });
    }

    /// <summary>
    /// Access to the X11-style "primary selection" (the middle-click paste
    /// buffer on Linux). On platforms without a primary selection this maps
    /// to an empty string and writes are silently no-ops.
    /// </summary>
    public static class PrimarySelection
    {
        /// <summary>
        /// True if the primary selection contains a non-empty text string.
        /// </summary>
        public static bool HasText
            => Application.Current.Invoke(static () => SDL.HasPrimarySelectionText());

        /// <summary>
        /// Gets or sets the UTF-8 text in the primary selection.
        /// </summary>
        public static string Text
        {
            get => Application.Current.Invoke(static () => SDL.GetPrimarySelectionText());
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Application.Current.Invoke(() => SDL.SetPrimarySelectionText(value));
            }
        }
    }
}

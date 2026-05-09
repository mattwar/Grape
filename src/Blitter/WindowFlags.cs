namespace Blitter;

/// <summary>
/// Creation-time flags for a <see cref="Window"/>.
/// </summary>
/// <remarks>
/// Only flags that cannot be changed after the window has been created are
/// represented here. Runtime-toggleable state (resizable, bordered, fullscreen,
/// always-on-top, grab, etc.) is exposed as properties on <see cref="Window"/>,
/// and transient state (minimized, maximized, hidden, focus, occlusion) is
/// exposed either as methods or as read-only properties.
///
/// Values mirror the underlying <c>SDL_WindowFlags</c> bit pattern so they
/// can be cast at the SDL boundary.
/// </remarks>
[Flags]
public enum WindowFlags : ulong
{
    /// <summary>No creation flags.</summary>
    None = 0,

    /// <summary>Window is usable with an OpenGL context.</summary>
    OpenGL = 0x0000000000000002,

    /// <summary>Window is wrapping a foreign HWND/NSView/etc.</summary>
    External = 0x0000000000000800,

    /// <summary>Window uses a high pixel density back buffer if possible.</summary>
    HighPixelDensity = 0x0000000000002000,

    /// <summary>
    /// Window should be treated as a utility window: not shown in the
    /// taskbar or window list.
    /// </summary>
    Utility = 0x0000000000020000,

    /// <summary>
    /// Window should be treated as a tooltip and does not get mouse or
    /// keyboard focus. Requires a parent window.
    /// </summary>
    Tooltip = 0x0000000000040000,

    /// <summary>
    /// Window should be treated as a popup menu. Requires a parent window.
    /// </summary>
    PopupMenu = 0x0000000000080000,

    /// <summary>Window is in fill-document mode (Emscripten only).</summary>
    FillDocument = 0x0000000000200000,

    /// <summary>Window is usable for a Vulkan surface.</summary>
    Vulkan = 0x0000000010000000,

    /// <summary>Window is usable for a Metal view.</summary>
    Metal = 0x0000000020000000,

    /// <summary>Window has a transparent buffer.</summary>
    Transparent = 0x0000000040000000,
}

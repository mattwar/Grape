namespace Blitter.Events;

/// <summary>
/// Handler for the per-frame <c>Rendering</c> event on a window. The
/// concrete window and its renderer are passed directly so handlers
/// don't have to cast.
/// </summary>
public delegate void WindowRenderingEventHandler<TWindow, TRenderer>(TWindow window, TRenderer renderer)
    where TWindow : Window;

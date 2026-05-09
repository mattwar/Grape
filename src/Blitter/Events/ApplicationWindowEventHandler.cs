namespace Blitter.Events;

/// <summary>
/// Handler for window-targeted events raised at the application level.
/// </summary>
public delegate void ApplicationWindowEventHandler<T>(Application sender, Window window, T args);

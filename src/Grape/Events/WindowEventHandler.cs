namespace Grape.Events;

/// <summary>
/// Handler for window-targeted events raised by a single window.
/// </summary>
public delegate void WindowEventHandler<T>(Window sender, T args);

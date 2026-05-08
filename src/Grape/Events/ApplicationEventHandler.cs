namespace Grape.Events;

/// <summary>
/// Handler for application-wide events that have no associated window.
/// </summary>
public delegate void ApplicationEventHandler<T>(Application sender, T args);

using Grape.Devices;

namespace Grape.Events;

public readonly record struct WindowDisplayChangedEventArgs(DisplayDevice? Display);

using Blitter.Devices;

namespace Blitter.Events;

public readonly record struct WindowDisplayChangedEventArgs(DisplayDevice? Display);

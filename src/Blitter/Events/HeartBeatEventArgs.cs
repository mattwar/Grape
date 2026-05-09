namespace Blitter.Events;

public record struct HeartBeatEventArgs(TimeSpan ElapsedSinceStart, TimeSpan ElapsedSinceLastBeat);

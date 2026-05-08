namespace Grape.Events;

public readonly record struct TextEditingEventArgs(string Text, int Start, int Length);

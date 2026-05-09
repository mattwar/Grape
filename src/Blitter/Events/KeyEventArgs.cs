namespace Blitter.Events;

public readonly record struct KeyEventArgs(
    Key Key,
    KeyModifiers Modifiers,
    bool IsDown,
    bool IsRepeat);

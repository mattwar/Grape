namespace Blitter.Bits;

/// <summary>
/// A single binding alternative for an <see cref="InputActions"/>
/// entry. Multiple bindings of the same shape may be attached to one
/// action; the action is satisfied when any of its bindings is
/// satisfied (logical OR).
/// </summary>
/// <remarks>
/// Users normally construct bindings indirectly via
/// <see cref="InputActions"/> <c>Bind*</c> methods. Direct
/// construction is exposed so settings UIs can iterate
/// <see cref="InputActions.GetBindings(string)"/> and pattern-match.
/// </remarks>
public abstract record InputBinding;

/// <summary>Layout-dependent virtual key (e.g. <see cref="Key.W"/>).</summary>
public sealed record KeyBinding(Key Key) : InputBinding;

/// <summary>Layout-independent physical key position
/// (e.g. <see cref="PhysicalKey.W"/>).</summary>
public sealed record PhysicalKeyBinding(PhysicalKey Key) : InputBinding;

/// <summary>A mouse button.</summary>
public sealed record MouseButtonBinding(MouseButton Button) : InputBinding;

/// <summary>A pair of keys producing a signed 1D direction
/// (<c>+1</c> if <see cref="Positive"/> held, <c>-1</c> if
/// <see cref="Negative"/> held).</summary>
public sealed record KeyDirectionBinding(Key Negative, Key Positive) : InputBinding;

/// <summary>Four keys producing a unit-length 2D direction
/// (diagonals normalized).</summary>
public sealed record KeyDirection2DBinding(
    Key Left, Key Right, Key Down, Key Up) : InputBinding;

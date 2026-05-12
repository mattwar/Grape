namespace Blitter.Bits;

/// <summary>
/// The shape of an <see cref="InputActions"/> entry. Determined by
/// the first <c>Bind*</c> call on an action name and locked thereafter
/// until <see cref="InputActions.Clear"/> or a <c>Rebind*</c> of a
/// different shape resets it.
/// </summary>
public enum InputActionKind
{
    /// <summary>No bindings yet; kind not determined.</summary>
    Unset,

    /// <summary>Held / pressed / released — boolean state.
    /// Bound to keys, mouse buttons, or (later) gamepad buttons.</summary>
    Digital,

    /// <summary>Signed 1D value in [-1, +1]. Bound to a key pair
    /// or (later) a gamepad stick channel.</summary>
    Direction,

    /// <summary>2D <see cref="System.Numerics.Vector2"/> value, unit-length
    /// when active. Bound to four keys or (later) a gamepad stick.</summary>
    Direction2D,
}

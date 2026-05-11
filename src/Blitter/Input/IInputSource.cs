namespace Blitter.Input;

/// <summary>
/// Abstraction over the live input device state read by
/// <see cref="FrameInput"/>. Production code uses the default
/// SDL-backed implementation; tests inject a fake to drive
/// edge-detection logic deterministically without standing up SDL.
/// </summary>
internal interface IInputSource
{
    /// <summary>Number of keyboard scancodes the source reports.</summary>
    int KeyCount { get; }

    /// <summary>
    /// Copies the current keyboard state (one bool per physical key) into
    /// <paramref name="destination"/>. <paramref name="destination"/> must
    /// have length >= <see cref="KeyCount"/>.
    /// </summary>
    void ReadKeyboardState(Span<bool> destination);

    /// <summary>
    /// Returns the current mouse button mask and global cursor position.
    /// </summary>
    void ReadMouseState(out MouseButtons buttons, out System.Numerics.Vector2 position);

    /// <summary>
    /// Returns the SDL relative-motion accumulator since the previous
    /// read. Must be drained every frame so a later RelativeMouseMode
    /// flip doesn't dump backlog into <see cref="FrameInput.MouseDelta"/>.
    /// </summary>
    System.Numerics.Vector2 ReadRelativeMouseMotion();

    /// <summary>True if any window currently has relative mouse mode on.</summary>
    bool IsAnyWindowRelativeMouseMode();
}

using System.Numerics;

namespace Blitter.Bits;

/// <summary>
/// Named action map over a <see cref="FrameInput"/>: bind game-level
/// verbs ("Jump", "Move", "Fire") to physical keys, mouse buttons, or
/// key combos, then query the action by name without your game logic
/// caring which input the user picked.
/// </summary>
/// <remarks>
/// <para>
/// Each action has a fixed <see cref="InputActionKind"/> determined by
/// the first <c>Bind*</c> call. Adding a second binding of a different
/// shape throws <see cref="InvalidOperationException"/>; querying with
/// the wrong shape (e.g. <see cref="GetDirection"/> on a digital
/// action) likewise throws. Use <c>Rebind*</c> to atomically replace
/// all bindings (and re-set the kind), or <see cref="Clear"/> to drop
/// the action.
/// </para>
/// <para>
/// Action names are case-insensitive. Looking up an unknown action
/// throws <see cref="KeyNotFoundException"/>; use
/// <see cref="Contains"/> for soft checks.
/// </para>
/// <para>
/// Edges are reported at the <em>action</em> level, not per binding:
/// holding Space while pressing W (both bound to "Jump") fires
/// <see cref="WasJustPressed"/> exactly once.
/// </para>
/// </remarks>
public sealed partial class InputActions
{
    private readonly FrameInput _input;
    private readonly Dictionary<string, ActionEntry> _actions =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new action map over the given <see cref="FrameInput"/>.
    /// All queries delegate to the same instance; advancing it advances
    /// all derived action edges.
    /// </summary>
    public InputActions(FrameInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _input = input;
    }

    /// <summary>The set of action names currently registered.</summary>
    public IReadOnlyCollection<string> Actions => _actions.Keys;

    /// <summary>True if an action with the given name exists.</summary>
    public bool Contains(string action) => _actions.ContainsKey(action);

    /// <summary>The <see cref="InputActionKind"/> of the given action.</summary>
    public InputActionKind GetKind(string action) => GetEntry(action).Kind;

    /// <summary>
    /// The bindings registered for the given action. Empty list if the
    /// action exists but has no bindings yet.
    /// </summary>
    public IReadOnlyList<InputBinding> GetBindings(string action) =>
        GetEntry(action).Bindings;

    // ============================================================
    //  Bind — append a binding (or set of bindings of the same shape)
    // ============================================================

    /// <summary>Adds one or more keys as digital bindings.</summary>
    public void Bind(string action, params Key[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var entry = EnsureEntry(action, InputActionKind.Digital);
        foreach (var k in keys)
            entry.Bindings.Add(new KeyBinding(k));
    }

    /// <summary>Adds one or more physical keys as digital bindings.</summary>
    public void Bind(string action, params PhysicalKey[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var entry = EnsureEntry(action, InputActionKind.Digital);
        foreach (var k in keys)
            entry.Bindings.Add(new PhysicalKeyBinding(k));
    }

    /// <summary>Adds one or more mouse buttons as digital bindings.</summary>
    public void Bind(string action, params MouseButton[] buttons)
    {
        ArgumentNullException.ThrowIfNull(buttons);
        var entry = EnsureEntry(action, InputActionKind.Digital);
        foreach (var b in buttons)
            entry.Bindings.Add(new MouseButtonBinding(b));
    }

    /// <summary>
    /// Adds a key-pair direction binding (signed 1D scalar).
    /// </summary>
    public void BindDirection(string action, Key negative, Key positive)
    {
        var entry = EnsureEntry(action, InputActionKind.Direction);
        entry.Bindings.Add(new KeyDirectionBinding(negative, positive));
    }

    /// <summary>
    /// Adds a four-key direction binding (unit-length 2D vector).
    /// </summary>
    public void BindDirection2D(string action,
        Key left, Key right, Key down, Key up)
    {
        var entry = EnsureEntry(action, InputActionKind.Direction2D);
        entry.Bindings.Add(new KeyDirection2DBinding(left, right, down, up));
    }

    // ============================================================
    //  Rebind — replace all bindings (may change the action's kind)
    // ============================================================

    /// <summary>Replaces all bindings on <paramref name="action"/> with
    /// the given digital key bindings.</summary>
    public void Rebind(string action, params Key[] keys)
    {
        Clear(action);
        Bind(action, keys);
    }

    /// <summary>Replaces all bindings on <paramref name="action"/> with
    /// the given physical-key digital bindings.</summary>
    public void Rebind(string action, params PhysicalKey[] keys)
    {
        Clear(action);
        Bind(action, keys);
    }

    /// <summary>Replaces all bindings on <paramref name="action"/> with
    /// the given mouse-button digital bindings.</summary>
    public void Rebind(string action, params MouseButton[] buttons)
    {
        Clear(action);
        Bind(action, buttons);
    }

    /// <summary>Replaces all bindings on <paramref name="action"/> with
    /// the given key-pair direction binding.</summary>
    public void RebindDirection(string action, Key negative, Key positive)
    {
        Clear(action);
        BindDirection(action, negative, positive);
    }

    /// <summary>Replaces all bindings on <paramref name="action"/> with
    /// the given four-key 2D direction binding.</summary>
    public void RebindDirection2D(string action,
        Key left, Key right, Key down, Key up)
    {
        Clear(action);
        BindDirection2D(action, left, right, down, up);
    }

    // ============================================================
    //  Clear — drop the action entirely (resets kind so it can be
    //  rebound to a different shape later).
    // ============================================================

    /// <summary>
    /// Removes the action and all its bindings. No-op if the action
    /// doesn't exist.
    /// </summary>
    public void Clear(string action)
    {
        _actions.Remove(action);
    }

    // ============================================================
    //  Digital queries
    // ============================================================

    /// <summary>
    /// True if any digital binding on <paramref name="action"/> is
    /// currently held.
    /// </summary>
    public bool IsPressed(string action)
    {
        var entry = GetEntry(action);
        RequireKind(entry, InputActionKind.Digital, nameof(IsPressed));
        return EvaluateDigitalCurrent(entry);
    }

    /// <summary>
    /// True on the frame the action transitioned from "no binding
    /// active" to "at least one binding active". Fires once even when
    /// multiple bindings rise simultaneously.
    /// </summary>
    public bool WasJustPressed(string action)
    {
        var entry = GetEntry(action);
        RequireKind(entry, InputActionKind.Digital, nameof(WasJustPressed));
        var curr = EvaluateDigitalCurrent(entry);
        var prev = EvaluateDigitalPrevious(entry);
        return curr && !prev;
    }

    /// <summary>
    /// True on the frame the action transitioned from "at least one
    /// binding active" to "no binding active".
    /// </summary>
    public bool WasJustReleased(string action)
    {
        var entry = GetEntry(action);
        RequireKind(entry, InputActionKind.Digital, nameof(WasJustReleased));
        var curr = EvaluateDigitalCurrent(entry);
        var prev = EvaluateDigitalPrevious(entry);
        return !curr && prev;
    }

    // ============================================================
    //  Direction queries
    // ============================================================

    /// <summary>
    /// Returns the bound direction with the greatest absolute value
    /// (so multiple key pairs can be OR'd: arrow keys + WASD both
    /// drive the same action).
    /// </summary>
    public float GetDirection(string action)
    {
        var entry = GetEntry(action);
        RequireKind(entry, InputActionKind.Direction, nameof(GetDirection));
        float best = 0f;
        foreach (var b in entry.Bindings)
        {
            if (b is KeyDirectionBinding d)
            {
                var v = _input.Direction(d.Negative, d.Positive);
                if (MathF.Abs(v) > MathF.Abs(best))
                    best = v;
            }
        }
        return best;
    }

    /// <summary>
    /// Returns the bound 2D direction with the greatest length. Each
    /// individual binding is already unit-length when active, so this
    /// just picks the first non-zero one (later bindings can win ties).
    /// </summary>
    public Vector2 GetDirection2D(string action)
    {
        var entry = GetEntry(action);
        RequireKind(entry, InputActionKind.Direction2D, nameof(GetDirection2D));
        Vector2 best = Vector2.Zero;
        float bestLenSq = 0f;
        foreach (var b in entry.Bindings)
        {
            if (b is KeyDirection2DBinding d)
            {
                var v = _input.Direction2D(d.Left, d.Right, d.Down, d.Up);
                var lenSq = v.LengthSquared();
                if (lenSq > bestLenSq)
                {
                    best = v;
                    bestLenSq = lenSq;
                }
            }
        }
        return best;
    }

    // ============================================================
    //  Internals
    // ============================================================

    private bool EvaluateDigitalCurrent(ActionEntry entry)
    {
        foreach (var b in entry.Bindings)
        {
            var hit = b switch
            {
                KeyBinding k => _input.IsDown(k.Key),
                PhysicalKeyBinding p => _input.IsDown(p.Key),
                MouseButtonBinding m => _input.IsDown(m.Button),
                _ => false,
            };
            if (hit) return true;
        }
        return false;
    }

    private bool EvaluateDigitalPrevious(ActionEntry entry)
    {
        foreach (var b in entry.Bindings)
        {
            var hit = b switch
            {
                KeyBinding k => _input.WasPreviouslyDown(k.Key),
                PhysicalKeyBinding p => _input.WasPreviouslyDown(p.Key),
                MouseButtonBinding m => _input.WasPreviouslyDown(m.Button),
                _ => false,
            };
            if (hit) return true;
        }
        return false;
    }

    private ActionEntry GetEntry(string action)
    {
        if (!_actions.TryGetValue(action, out var entry))
            throw new KeyNotFoundException(
                $"InputActions has no action named '{action}'. " +
                "Call Bind*(...) first, or use Contains(name) for a soft check.");
        return entry;
    }

    private ActionEntry EnsureEntry(string action, InputActionKind kind)
    {
        if (!_actions.TryGetValue(action, out var entry))
        {
            entry = new ActionEntry { Kind = kind };
            _actions[action] = entry;
            return entry;
        }
        if (entry.Kind != kind)
        {
            throw new InvalidOperationException(
                $"Action '{action}' is already bound as {entry.Kind}; " +
                $"cannot add a {kind} binding. Use a Rebind* method to " +
                "atomically replace bindings and change the action's kind.");
        }
        return entry;
    }

    private static void RequireKind(
        ActionEntry entry, InputActionKind expected, string method)
    {
        if (entry.Kind != expected)
            throw new InvalidOperationException(
                $"{method} requires a {expected} action, but the bound " +
                $"action is {entry.Kind}.");
    }

    internal sealed class ActionEntry
    {
        public InputActionKind Kind { get; set; }
        public List<InputBinding> Bindings { get; } = new();
    }
}

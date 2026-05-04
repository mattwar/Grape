namespace Grape.Shaders;

/// <summary>
/// Root of the shader IR. Every node carries diagnostics and an aggregated
/// <see cref="ContainsState"/> flag set computed at construction time and
/// derived from this node's own facts plus its descendants'.
/// </summary>
/// <remarks>
/// Nodes are immutable. Refinement (e.g. setting a result type during
/// binding, or assigning a layout location) is monotonic: it always
/// produces a new instance via a <c>With*</c> method that copy-constructs
/// the node with the additional fact filled in.
/// </remarks>
public abstract class ShaderElement
{
    private readonly ContainsState _state;

    /// <summary>Diagnostics attached directly to this element.</summary>
    public ImmutableList<ShaderDiagnostic> Diagnostics { get; }

    /// <summary>True if this element or any descendant is missing a binding fact.</summary>
    public bool IsUnbound => (_state & ContainsState.Unbound) != 0;

    /// <summary>True if this element or any descendant has diagnostics.</summary>
    public bool ContainsDiagnostics => (_state & ContainsState.Diagnostics) != 0;

    /// <summary>True if <see cref="Diagnostics"/> is non-empty on this element.</summary>
    public bool HasDiagnostics => Diagnostics.Count > 0;

    private protected ShaderElement(
        ContainsState state,
        ImmutableList<ShaderDiagnostic>? diagnostics)
    {
        Diagnostics = diagnostics ?? ImmutableList<ShaderDiagnostic>.Empty;
        if (Diagnostics.Count > 0) state |= ContainsState.Diagnostics;
        _state = state;
    }

    /// <summary>The number of immediate child elements, including optional ones that may be null.</summary>
    public abstract int ChildCount { get; }

    /// <summary>Returns the child at <paramref name="index"/>, or null for an absent optional child.</summary>
    public abstract ShaderElement? GetChild(int index);

    /// <summary>Rewrites children using <paramref name="rewriter"/>; returns this if no child changed.</summary>
    public abstract ShaderElement RewriteChildren(ShaderRewriter rewriter);

    /// <summary>Returns this element with its <see cref="Diagnostics"/> replaced.</summary>
    public abstract ShaderElement WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics);

    /// <summary>
    /// Walks all descendants (and this element) gathering every diagnostic
    /// present in the tree. Cheap when <see cref="ContainsDiagnostics"/> is
    /// false (returns an empty list without walking).
    /// </summary>
    public ImmutableList<ShaderDiagnostic> GetContainedDiagnostics()
    {
        if (!ContainsDiagnostics) return ImmutableList<ShaderDiagnostic>.Empty;
        var builder = ImmutableList.CreateBuilder<ShaderDiagnostic>();
        Collect(this, builder);
        return builder.ToImmutable();

        static void Collect(ShaderElement e, ImmutableList<ShaderDiagnostic>.Builder b)
        {
            if (!e.ContainsDiagnostics) return;
            if (e.HasDiagnostics) b.AddRange(e.Diagnostics);
            for (int i = 0, n = e.ChildCount; i < n; i++)
                if (e.GetChild(i) is { } c) Collect(c, b);
        }
    }

    /// <summary>State that aggregates from leaves to root.</summary>
    [Flags]
    private protected enum ContainsState
    {
        None = 0,
        Unbound = 1,
        Diagnostics = 2,
    }

    /// <summary>Asserts that a binding fact is present; <see cref="ContainsState.Unbound"/> if null.</summary>
    private protected static ContainsState NotNullState(object? value)
        => value is null ? ContainsState.Unbound : ContainsState.None;

    /// <summary>Reads the aggregate state of an element (None for null).</summary>
    private protected static ContainsState State(ShaderElement? element)
        => element is null ? ContainsState.None : element._state;

    /// <summary>Combines the aggregate state of every element in <paramref name="elements"/>.</summary>
    private protected static ContainsState CombineState<T>(ImmutableArray<T> elements) where T : ShaderElement
    {
        var s = ContainsState.None;
        if (elements.IsDefault) return s;
        for (int i = 0; i < elements.Length; i++) s |= State(elements[i]);
        return s;
    }

    /// <summary>Combines the aggregate state of every element in <paramref name="elements"/>.</summary>
    private protected static ContainsState CombineState<T>(IEnumerable<T>? elements) where T : ShaderElement
    {
        if (elements is null) return ContainsState.None;
        var s = ContainsState.None;
        foreach (var e in elements) s |= State(e);
        return s;
    }
}

namespace Grape.Shaders;

/// <summary>
/// Read-only walks over <see cref="ShaderElement"/> trees. Built on
/// <see cref="ShaderElement.ChildCount"/> + <see cref="ShaderElement.GetChild(int)"/>
/// so they cover every element kind without a per-kind visitor.
/// </summary>
public static class ShaderElementExtensions
{
    /// <summary>Pre-order walk; invokes <paramref name="action"/> for each element.</summary>
    public static void Walk(this ShaderElement? root, Action<ShaderElement> action)
        => Walk(root, _ => true, action);

    /// <summary>
    /// Pre-order walk; invokes <paramref name="action"/> for each element and
    /// only descends into children when <paramref name="walkChildren"/> returns true.
    /// </summary>
    public static void Walk(
        this ShaderElement? root,
        Func<ShaderElement, bool> walkChildren,
        Action<ShaderElement> action)
    {
        if (root is null) return;
        action(root);
        if (!walkChildren(root)) return;
        for (int i = 0, n = root.ChildCount; i < n; i++)
            if (root.GetChild(i) is { } c)
                Walk(c, walkChildren, action);
    }

    /// <summary>Returns every element in the tree matching <paramref name="predicate"/>.</summary>
    public static IReadOnlyList<ShaderElement> Where(
        this ShaderElement root,
        Func<ShaderElement, bool> predicate)
    {
        var list = new List<ShaderElement>();
        Walk(root, e => { if (predicate(e)) list.Add(e); });
        return list;
    }

    /// <summary>Projects every matching element via <paramref name="selector"/>.</summary>
    public static IReadOnlyList<TValue> SelectWhere<TValue>(
        this ShaderElement root,
        Func<ShaderElement, bool> predicate,
        Func<ShaderElement, TValue> selector)
    {
        var list = new List<TValue>();
        Walk(root, e => { if (predicate(e)) list.Add(selector(e)); });
        return list;
    }

    /// <summary>
    /// Returns the first descendant (or the root itself) of type
    /// <typeparamref name="TNode"/> matching <paramref name="predicate"/>,
    /// or null if none found.
    /// </summary>
    public static TNode? FirstDescendantOrSelf<TNode>(
        this ShaderElement? root,
        Func<TNode, bool>? predicate = null)
        where TNode : ShaderElement
    {
        if (root is null) return null;
        if (root is TNode t && (predicate is null || predicate(t))) return t;
        for (int i = 0, n = root.ChildCount; i < n; i++)
        {
            if (root.GetChild(i) is { } c)
            {
                var found = FirstDescendantOrSelf(c, predicate);
                if (found is not null) return found;
            }
        }
        return null;
    }
}

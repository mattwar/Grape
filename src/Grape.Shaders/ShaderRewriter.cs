namespace Grape.Shaders;

/// <summary>
/// Rewrites a tree of <see cref="ShaderElement"/>s. Override
/// <see cref="Rewrite(ShaderElement, ShaderElement)"/> (called after children
/// have been rewritten) to transform individual nodes; the default is identity.
/// </summary>
public abstract class ShaderRewriter
{
    /// <summary>Rewrites <paramref name="node"/>: first its children, then itself.</summary>
    public virtual ShaderElement? Rewrite(ShaderElement? node)
    {
        if (node is null) return null;
        var current = node.RewriteChildren(this);
        return Rewrite(current, node);
    }

    /// <summary>
    /// Rewrites every element of <paramref name="nodes"/>; returns the same
    /// instance when no element changed, otherwise a new array.
    /// </summary>
    public virtual ImmutableArray<T> Rewrite<T>(ImmutableArray<T> nodes) where T : ShaderElement
    {
        if (nodes.IsDefault) return nodes;
        ImmutableArray<T>.Builder? builder = null;
        for (int i = 0; i < nodes.Length; i++)
        {
            var rewritten = (T?)Rewrite(nodes[i]);
            if (builder is null && !ReferenceEquals(rewritten, nodes[i]))
            {
                builder = ImmutableArray.CreateBuilder<T>(nodes.Length);
                for (int j = 0; j < i; j++) builder.Add(nodes[j]);
            }
            if (builder is not null && rewritten is not null)
                builder.Add(rewritten);
        }
        return builder?.ToImmutable() ?? nodes;
    }

    /// <summary>
    /// Hook called after a node's children have been rewritten. Default returns
    /// <paramref name="current"/> unchanged. <paramref name="original"/> is
    /// the node before its children were rewritten.
    /// </summary>
    protected virtual ShaderElement Rewrite(ShaderElement current, ShaderElement original) => current;
}

public static class ShaderRewriterExtensions
{
    /// <summary>Rewrites every node of type <typeparamref name="TNode"/> in the tree.</summary>
    public static ShaderElement RewriteAll<TNode>(
        this ShaderElement root,
        Func<TNode, ShaderElement> rewrite)
        where TNode : ShaderElement
        => new TypedRewriter<TNode>((c, _) => rewrite(c)).Rewrite(root)!;

    /// <summary>Rewrites every node of type <typeparamref name="TNode"/>; receives both current and original.</summary>
    public static ShaderElement RewriteAll<TNode>(
        this ShaderElement root,
        Func<TNode, TNode, ShaderElement> rewrite)
        where TNode : ShaderElement
        => new TypedRewriter<TNode>(rewrite).Rewrite(root)!;

    private sealed class TypedRewriter<TNode>(Func<TNode, TNode, ShaderElement> rewrite) : ShaderRewriter
        where TNode : ShaderElement
    {
        protected override ShaderElement Rewrite(ShaderElement current, ShaderElement original)
            => current is TNode tcur && original is TNode torig ? rewrite(tcur, torig) : current;
    }
}

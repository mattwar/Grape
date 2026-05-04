namespace Grape.Shaders;

/// <summary>
/// A sequence of expressions. The block's value is the value of its final
/// expression; blocks with no body or a void final expression are themselves
/// void.
/// </summary>
public sealed class BlockExpression : ShaderExpression
{
    public ImmutableArray<ShaderExpression> Body { get; }

    public BlockExpression(ImmutableArray<ShaderExpression> body)
        : this(body, null, null) { }

    private BlockExpression(
        ImmutableArray<ShaderExpression> body,
        ShaderType? resultType,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(CombineState(body), resultType, diagnostics)
    {
        Body = body;
    }

    public override BlockExpression WithResultType(ShaderType? resultType)
        => ReferenceEquals(resultType, ResultType) ? this
            : new BlockExpression(Body, resultType, Diagnostics);

    public override BlockExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new BlockExpression(Body, ResultType, diagnostics);

    public BlockExpression WithBody(ImmutableArray<ShaderExpression> body)
        => body == Body ? this : new BlockExpression(body, ResultType, Diagnostics);

    public override int ChildCount => Body.Length;
    public override ShaderElement? GetChild(int index) => Body[index];

    public override BlockExpression RewriteChildren(ShaderRewriter rewriter)
        => WithBody(rewriter.Rewrite(Body));
}

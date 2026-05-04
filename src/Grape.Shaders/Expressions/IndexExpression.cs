namespace Grape.Shaders;

/// <summary>Dynamic index into a vector, matrix column, or array.</summary>
public sealed class IndexExpression : ShaderExpression
{
    public ShaderExpression Source { get; }
    public ShaderExpression Index  { get; }

    public IndexExpression(ShaderExpression source, ShaderExpression index)
        : this(source, index, null, null) { }

    private IndexExpression(
        ShaderExpression source,
        ShaderExpression index,
        ShaderType? resultType,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(State(source) | State(index), resultType, diagnostics)
    {
        Source = source;
        Index = index;
    }

    public override IndexExpression WithResultType(ShaderType? resultType)
        => ReferenceEquals(resultType, ResultType) ? this
            : new IndexExpression(Source, Index, resultType, Diagnostics);

    public override IndexExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new IndexExpression(Source, Index, ResultType, diagnostics);

    public IndexExpression WithOperands(ShaderExpression source, ShaderExpression index)
        => ReferenceEquals(source, Source) && ReferenceEquals(index, Index) ? this
            : new IndexExpression(source, index, ResultType, Diagnostics);

    public override int ChildCount => 2;
    public override ShaderElement? GetChild(int index) => index switch
    {
        0 => Source,
        1 => Index,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public override IndexExpression RewriteChildren(ShaderRewriter rewriter)
    {
        var s = (ShaderExpression)rewriter.Rewrite(Source)!;
        var i = (ShaderExpression)rewriter.Rewrite(Index)!;
        return WithOperands(s, i);
    }
}

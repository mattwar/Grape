namespace Grape.Shaders;

/// <summary>A scalar literal. <see cref="Value"/> is a boxed bool / int / uint / float matching <see cref="ShaderExpression.ResultType"/>.</summary>
public sealed class LiteralExpression : ShaderExpression
{
    public object Value { get; }

    public LiteralExpression(ShaderType type, object value) : this(type, value, null) { }

    private LiteralExpression(ShaderType? type, object value, ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(ContainsState.None, type, diagnostics)
    {
        Value = value;
    }

    public override LiteralExpression WithResultType(ShaderType? resultType)
        => ReferenceEquals(resultType, ResultType) ? this : new LiteralExpression(resultType, Value, Diagnostics);

    public override LiteralExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this : new LiteralExpression(ResultType, Value, diagnostics);

    public override int ChildCount => 0;
    public override ShaderElement? GetChild(int index) => throw new ArgumentOutOfRangeException(nameof(index));
    public override LiteralExpression RewriteChildren(ShaderRewriter rewriter) => this;
}

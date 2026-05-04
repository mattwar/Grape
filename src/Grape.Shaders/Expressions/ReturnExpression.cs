namespace Grape.Shaders;

/// <summary>Returns from the enclosing function. <see cref="Value"/> is null for void returns.</summary>
public sealed class ReturnExpression : ShaderExpression
{
    public ShaderExpression? Value { get; }

    public ReturnExpression(ShaderExpression? value = null) : this(value, null) { }

    private ReturnExpression(ShaderExpression? value, ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(State(value), ShaderTypeSystem.Void, diagnostics)
    {
        Value = value;
    }

    public override ReturnExpression WithResultType(ShaderType? resultType) => this;

    public override ReturnExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this : new ReturnExpression(Value, diagnostics);

    public ReturnExpression WithValue(ShaderExpression? value)
        => ReferenceEquals(value, Value) ? this : new ReturnExpression(value, Diagnostics);

    public override int ChildCount => 1;
    public override ShaderElement? GetChild(int index) => index == 0
        ? Value
        : throw new ArgumentOutOfRangeException(nameof(index));

    public override ReturnExpression RewriteChildren(ShaderRewriter rewriter)
        => WithValue((ShaderExpression?)rewriter.Rewrite(Value));
}

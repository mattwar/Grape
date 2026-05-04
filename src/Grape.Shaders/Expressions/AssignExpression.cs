namespace Grape.Shaders;

/// <summary>
/// Assignment. Yields the assigned value's type (so it can be embedded in a
/// larger expression). Target must be a parameter / global / swizzle / index /
/// field-access of a mutable target.
/// </summary>
public sealed class AssignExpression : ShaderExpression
{
    public ShaderExpression Target { get; }
    public ShaderExpression Value  { get; }

    public AssignExpression(ShaderExpression target, ShaderExpression value)
        : this(target, value, null, null) { }

    private AssignExpression(
        ShaderExpression target,
        ShaderExpression value,
        ShaderType? resultType,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(State(target) | State(value), resultType, diagnostics)
    {
        Target = target;
        Value = value;
    }

    public override AssignExpression WithResultType(ShaderType? resultType)
        => ReferenceEquals(resultType, ResultType) ? this
            : new AssignExpression(Target, Value, resultType, Diagnostics);

    public override AssignExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new AssignExpression(Target, Value, ResultType, diagnostics);

    public AssignExpression WithOperands(ShaderExpression target, ShaderExpression value)
        => ReferenceEquals(target, Target) && ReferenceEquals(value, Value) ? this
            : new AssignExpression(target, value, ResultType, Diagnostics);

    public override int ChildCount => 2;
    public override ShaderElement? GetChild(int index) => index switch
    {
        0 => Target,
        1 => Value,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public override AssignExpression RewriteChildren(ShaderRewriter rewriter)
    {
        var t = (ShaderExpression)rewriter.Rewrite(Target)!;
        var v = (ShaderExpression)rewriter.Rewrite(Value)!;
        return WithOperands(t, v);
    }
}

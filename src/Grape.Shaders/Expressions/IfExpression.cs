namespace Grape.Shaders;

/// <summary>
/// Conditional. Subsumes both classic if-statements (no else, void) and
/// ternary expressions (with else and a common arm type).
/// </summary>
public sealed class IfExpression : ShaderExpression
{
    public ShaderExpression  Test    { get; }
    public ShaderExpression  IfTrue  { get; }
    public ShaderExpression? IfFalse { get; }

    public IfExpression(ShaderExpression test, ShaderExpression ifTrue, ShaderExpression? ifFalse = null)
        : this(test, ifTrue, ifFalse, null, null) { }

    private IfExpression(
        ShaderExpression test,
        ShaderExpression ifTrue,
        ShaderExpression? ifFalse,
        ShaderType? resultType,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(State(test) | State(ifTrue) | State(ifFalse), resultType, diagnostics)
    {
        Test = test;
        IfTrue = ifTrue;
        IfFalse = ifFalse;
    }

    public override IfExpression WithResultType(ShaderType? resultType)
        => ReferenceEquals(resultType, ResultType) ? this
            : new IfExpression(Test, IfTrue, IfFalse, resultType, Diagnostics);

    public override IfExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new IfExpression(Test, IfTrue, IfFalse, ResultType, diagnostics);

    public IfExpression WithChildren(ShaderExpression test, ShaderExpression ifTrue, ShaderExpression? ifFalse)
        => ReferenceEquals(test, Test) && ReferenceEquals(ifTrue, IfTrue) && ReferenceEquals(ifFalse, IfFalse) ? this
            : new IfExpression(test, ifTrue, ifFalse, ResultType, Diagnostics);

    public override int ChildCount => 3;
    public override ShaderElement? GetChild(int index) => index switch
    {
        0 => Test,
        1 => IfTrue,
        2 => IfFalse,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public override IfExpression RewriteChildren(ShaderRewriter rewriter)
    {
        var t = (ShaderExpression)rewriter.Rewrite(Test)!;
        var a = (ShaderExpression)rewriter.Rewrite(IfTrue)!;
        var b = (ShaderExpression?)rewriter.Rewrite(IfFalse);
        return WithChildren(t, a, b);
    }
}

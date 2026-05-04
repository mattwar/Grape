namespace Grape.Shaders;

/// <summary>Bounded for-loop: <c>for (var i = init; test; i = step) body</c>. Always void-typed.</summary>
public sealed class ForExpression : ShaderExpression
{
    public ParameterExpression Variable { get; }
    public ShaderExpression Initial { get; }
    public ShaderExpression Test    { get; }
    public ShaderExpression Step    { get; }
    public ShaderExpression Body    { get; }

    public ForExpression(
        ParameterExpression variable,
        ShaderExpression initial,
        ShaderExpression test,
        ShaderExpression step,
        ShaderExpression body)
        : this(variable, initial, test, step, body, null) { }

    private ForExpression(
        ParameterExpression variable,
        ShaderExpression initial,
        ShaderExpression test,
        ShaderExpression step,
        ShaderExpression body,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(
            State(variable) | State(initial) | State(test) | State(step) | State(body),
            ShaderTypeSystem.Void,
            diagnostics)
    {
        Variable = variable;
        Initial = initial;
        Test = test;
        Step = step;
        Body = body;
    }

    public override ForExpression WithResultType(ShaderType? resultType) => this;

    public override ForExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new ForExpression(Variable, Initial, Test, Step, Body, diagnostics);

    public ForExpression WithChildren(
        ParameterExpression variable,
        ShaderExpression initial,
        ShaderExpression test,
        ShaderExpression step,
        ShaderExpression body)
        => ReferenceEquals(variable, Variable)
            && ReferenceEquals(initial, Initial)
            && ReferenceEquals(test, Test)
            && ReferenceEquals(step, Step)
            && ReferenceEquals(body, Body)
                ? this
                : new ForExpression(variable, initial, test, step, body, Diagnostics);

    public override int ChildCount => 5;
    public override ShaderElement? GetChild(int index) => index switch
    {
        0 => Variable,
        1 => Initial,
        2 => Test,
        3 => Step,
        4 => Body,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public override ForExpression RewriteChildren(ShaderRewriter rewriter)
    {
        var v = (ParameterExpression)rewriter.Rewrite(Variable)!;
        var init = (ShaderExpression)rewriter.Rewrite(Initial)!;
        var t = (ShaderExpression)rewriter.Rewrite(Test)!;
        var s = (ShaderExpression)rewriter.Rewrite(Step)!;
        var b = (ShaderExpression)rewriter.Rewrite(Body)!;
        return WithChildren(v, init, t, s, b);
    }
}

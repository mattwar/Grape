namespace Grape.Shaders;

public sealed class WhileExpression : ShaderExpression
{
    public ShaderExpression Test { get; }
    public ShaderExpression Body { get; }

    public WhileExpression(ShaderExpression test, ShaderExpression body)
        : this(test, body, null) { }

    private WhileExpression(
        ShaderExpression test,
        ShaderExpression body,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(State(test) | State(body), ShaderTypeSystem.Void, diagnostics)
    {
        Test = test;
        Body = body;
    }

    public override WhileExpression WithResultType(ShaderType? resultType) => this;

    public override WhileExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new WhileExpression(Test, Body, diagnostics);

    public WhileExpression WithChildren(ShaderExpression test, ShaderExpression body)
        => ReferenceEquals(test, Test) && ReferenceEquals(body, Body) ? this
            : new WhileExpression(test, body, Diagnostics);

    public override int ChildCount => 2;
    public override ShaderElement? GetChild(int index) => index switch
    {
        0 => Test,
        1 => Body,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public override WhileExpression RewriteChildren(ShaderRewriter rewriter)
    {
        var t = (ShaderExpression)rewriter.Rewrite(Test)!;
        var b = (ShaderExpression)rewriter.Rewrite(Body)!;
        return WithChildren(t, b);
    }
}

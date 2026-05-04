namespace Grape.Shaders;

public sealed class BreakExpression : ShaderExpression
{
    public BreakExpression() : this(null) { }

    private BreakExpression(ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(ContainsState.None, ShaderTypeSystem.Void, diagnostics) { }

    public override BreakExpression WithResultType(ShaderType? resultType) => this;

    public override BreakExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this : new BreakExpression(diagnostics);

    public override int ChildCount => 0;
    public override ShaderElement? GetChild(int index) => throw new ArgumentOutOfRangeException(nameof(index));
    public override BreakExpression RewriteChildren(ShaderRewriter rewriter) => this;
}

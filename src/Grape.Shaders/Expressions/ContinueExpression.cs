namespace Grape.Shaders;

public sealed class ContinueExpression : ShaderExpression
{
    public ContinueExpression() : this(null) { }

    private ContinueExpression(ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(ContainsState.None, ShaderTypeSystem.Void, diagnostics) { }

    public override ContinueExpression WithResultType(ShaderType? resultType) => this;

    public override ContinueExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this : new ContinueExpression(diagnostics);

    public override int ChildCount => 0;
    public override ShaderElement? GetChild(int index) => throw new ArgumentOutOfRangeException(nameof(index));
    public override ContinueExpression RewriteChildren(ShaderRewriter rewriter) => this;
}

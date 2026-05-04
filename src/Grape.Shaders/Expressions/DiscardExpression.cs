namespace Grape.Shaders;

/// <summary>
/// The fragment-shader <c>discard</c> / <c>OpKill</c> control-flow instruction.
/// Aborts the current fragment-shader invocation; no operand.
/// </summary>
public sealed class DiscardExpression : ShaderExpression
{
    public DiscardExpression() : this(null) { }

    private DiscardExpression(ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(ContainsState.None, ShaderTypeSystem.Void, diagnostics) { }

    public override DiscardExpression WithResultType(ShaderType? resultType) => this;

    public override DiscardExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this : new DiscardExpression(diagnostics);

    public override int ChildCount => 0;
    public override ShaderElement? GetChild(int index) => throw new ArgumentOutOfRangeException(nameof(index));
    public override DiscardExpression RewriteChildren(ShaderRewriter rewriter) => this;
}

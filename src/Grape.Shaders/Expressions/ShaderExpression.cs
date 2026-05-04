namespace Grape.Shaders;

/// <summary>
/// Base class for expressions. Adds the <see cref="ResultType"/> binding fact
/// to <see cref="ShaderElement"/>. Null result type means "not yet inferred"
/// and contributes <see cref="ShaderElement.IsUnbound"/>.
/// </summary>
public abstract class ShaderExpression : ShaderElement
{
    public ShaderType? ResultType { get; }

    private protected ShaderExpression(
        ContainsState state,
        ShaderType? resultType,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(state | NotNullState(resultType), diagnostics)
    {
        ResultType = resultType;
    }

    /// <summary>Returns this expression with its <see cref="ResultType"/> replaced.</summary>
    public abstract ShaderExpression WithResultType(ShaderType? resultType);

    public override abstract ShaderExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics);
    public override abstract ShaderExpression RewriteChildren(ShaderRewriter rewriter);
}

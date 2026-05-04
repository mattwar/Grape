namespace Grape.Shaders;

/// <summary>
/// Construct a vector, matrix, or struct from components. The constructed
/// type is fixed by the user (unlike most other expressions, this node's
/// result type is required at construction).
/// </summary>
public sealed class ConstructExpression : ShaderExpression
{
    public ImmutableArray<ShaderExpression> Args { get; }

    public ConstructExpression(ShaderType type, ImmutableArray<ShaderExpression> args)
        : this(type, args, null) { }

    private ConstructExpression(
        ShaderType? type,
        ImmutableArray<ShaderExpression> args,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(CombineState(args), type, diagnostics)
    {
        Args = args;
    }

    public override ConstructExpression WithResultType(ShaderType? resultType)
        => ReferenceEquals(resultType, ResultType) ? this
            : new ConstructExpression(resultType, Args, Diagnostics);

    public override ConstructExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new ConstructExpression(ResultType, Args, diagnostics);

    public ConstructExpression WithArgs(ImmutableArray<ShaderExpression> args)
        => args == Args ? this : new ConstructExpression(ResultType, args, Diagnostics);

    public override int ChildCount => Args.Length;
    public override ShaderElement? GetChild(int index) => Args[index];

    public override ConstructExpression RewriteChildren(ShaderRewriter rewriter)
        => WithArgs(rewriter.Rewrite(Args));
}

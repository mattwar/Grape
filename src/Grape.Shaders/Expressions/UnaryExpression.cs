namespace Grape.Shaders;

public sealed class UnaryExpression : ShaderExpression
{
    public ShaderUnaryOp Op { get; }
    public ShaderExpression Operand { get; }

    public UnaryExpression(ShaderUnaryOp op, ShaderExpression operand)
        : this(op, operand, null, null) { }

    private UnaryExpression(
        ShaderUnaryOp op,
        ShaderExpression operand,
        ShaderType? resultType,
        ImmutableList<ShaderDiagnostic>? diagnostics)
        : base(State(operand), resultType, diagnostics)
    {
        Op = op;
        Operand = operand;
    }

    public override UnaryExpression WithResultType(ShaderType? resultType)
        => ReferenceEquals(resultType, ResultType) ? this
            : new UnaryExpression(Op, Operand, resultType, Diagnostics);

    public override UnaryExpression WithDiagnostics(ImmutableList<ShaderDiagnostic> diagnostics)
        => diagnostics == Diagnostics ? this
            : new UnaryExpression(Op, Operand, ResultType, diagnostics);

    public UnaryExpression WithOperand(ShaderExpression operand)
        => ReferenceEquals(operand, Operand) ? this
            : new UnaryExpression(Op, operand, ResultType, Diagnostics);

    public override int ChildCount => 1;
    public override ShaderElement? GetChild(int index) => index == 0
        ? Operand
        : throw new ArgumentOutOfRangeException(nameof(index));

    public override UnaryExpression RewriteChildren(ShaderRewriter rewriter)
        => WithOperand((ShaderExpression)rewriter.Rewrite(Operand)!);
}
